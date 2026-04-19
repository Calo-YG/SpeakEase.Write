using System.Text.Json;
using System.Text.Json.Serialization;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// Plan-and-Execute 策略：先让 LLM 制定计划，再逐步执行计划中的步骤。
    /// 
    /// 执行流程：
    /// 1. Planning 阶段：调用 LLM 生成步骤列表（JSON 格式）
    /// 2. Execution 阶段：逐步执行每个步骤，每步内含 ReAct 式工具调用循环
    /// 3. Synthesis 阶段：所有步骤完成后，调用 LLM 综合所有观察结果生成最终回答
    /// 
    /// 与 ReAct 的区别：
    /// - ReAct：边推理边行动，每轮都可能调用工具
    /// - PlanAndExecute：先规划再执行，计划阶段不调用工具，执行阶段按计划走
    /// 
    /// 适用于：
    /// - 复杂创作任务（先列大纲，再逐章写）
    /// - 多步骤分析（先搜索 → 再整理 → 最后总结）
    /// - 需要"先想清楚再做"的长链推理
    /// </summary>
    public class PlanAndExecuteStrategy : IAgentLoopStrategy
    {
        /// <summary>
        /// 执行阶段每步的最大迭代次数（含工具调用循环）。
        /// 默认 5 次，足够完成单步骤内的工具调用。
        /// </summary>
        public int MaxIterationsPerStep { get; set; } = 5;

        /// <summary>
        /// 最大计划步骤数。超过此数量的步骤会被截断。
        /// 默认 10 步。
        /// </summary>
        public int MaxPlanSteps { get; set; } = 10;

        /// <summary>
        /// 是否在 Planning 阶段结束后发送计划内容作为 StreamChunk。
        /// 流式模式下设为 true 可让调用方提前看到计划。
        /// </summary>
        public bool EmitPlanInStream { get; set; } = true;

        /// <summary>
        /// Planning 阶段提示词模板。{0} = 步骤数范围，{1} = 原始 SystemPrompt。
        /// 默认为中文模板，可替换为英文或其他语言。
        /// </summary>
        public string PlanPromptTemplate { get; set; } =
            "你是一个任务规划专家。你的职责是将用户的请求分解为清晰的执行步骤。\n" +
            "输出格式要求：严格输出 JSON，不要输出其他内容。\n" +
            "格式如下：\n" +
            "{\n" +
            "  \"steps\": [\"步骤1的描述\", \"步骤2的描述\", \"步骤3的描述\"]\n" +
            "}\n" +
            "每个步骤应该是独立的、可执行的动作。步骤数量控制在 {0} 步之间。";

        /// <summary>
        /// Execution 阶段提示词模板。{0} = 当前步骤序号，{1} = 总步骤数，{2} = 步骤描述，{3} = 原始 SystemPrompt。
        /// </summary>
        public string StepPromptTemplate { get; set; } =
            "你正在执行一个多步骤计划中的第 {0}/{1} 步。\n当前步骤：{2}\n请专注于完成当前步骤，输出该步骤的执行结果。如果需要使用工具，请调用合适的工具。";

        /// <summary>
        /// Synthesis 阶段提示词模板。{0} = 步骤摘要，{1} = 原始 SystemPrompt。
        /// </summary>
        public string SynthesisPromptTemplate { get; set; } =
            "你已经完成了所有计划步骤的执行。现在请综合所有步骤的结果，生成一个连贯、完整的最终回答。\n\n{0}";

        /// <summary>
        /// Planning 阶段建议的步骤数范围。
        /// </summary>
        public string StepCountRange { get; set; } = "3-8";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <inheritdoc />
        public async Task<AgentResponse> ExecuteAsync(
            IAgentLoopContext context,
            AgentRequest request,
            CancellationToken cancellationToken)
        {
            var prepared = await context.PrepareRequestAsync(request, cancellationToken);
            var messages = new List<AgentMessage>(prepared.Messages);
            var allToolResults = new List<ToolResult>();
            var totalIterations = 0;

            // === Phase 1: Planning ===
            var planRequest = BuildPlanRequest(prepared, PlanPromptTemplate, StepCountRange);
            var planResponse = await context.LLMBackend.CompleteAsync(planRequest, cancellationToken);
            totalIterations++;

            var plan = ParsePlan(planResponse.Content);
            if (plan.Steps is null || plan.Steps.Count == 0)
            {
                // LLM 未返回有效计划，直接返回 LLM 的原始回复
                planResponse.StopReason = "no_plan";
                planResponse.Iterations = totalIterations;
                planResponse.ConversationHistory = messages;
                planResponse.ToolResults = allToolResults;
                return planResponse;
            }

            // 截断过多步骤
            if (plan.Steps.Count > MaxPlanSteps)
            {
                plan.Steps = plan.Steps.Take(MaxPlanSteps).ToList();
            }

            // 追加计划消息到对话历史
            messages.Add(new AgentMessage("assistant", planResponse.Content ?? string.Empty));

            // === Phase 2: Execution ===
            var stepObservations = new List<StepObservation>();

            for (var stepIndex = 0; stepIndex < plan.Steps.Count; stepIndex++)
            {
                var step = plan.Steps[stepIndex];
                var stepMessages = new List<AgentMessage>(messages);

                // 构建步骤执行请求
                var stepRequest = BuildStepRequest(prepared, stepMessages, step, stepIndex, plan.Steps.Count);

                // 执行步骤（含工具调用循环，类似 ReAct 但迭代次数有限）
                var (stepResult, stepToolResults, stepIterations) = await ExecuteStepWithToolsAsync(
                    context, stepRequest, stepMessages, cancellationToken);

                totalIterations += stepIterations;
                allToolResults.AddRange(stepToolResults);

                // 记录观察结果
                var observation = new StepObservation
                {
                    StepIndex = stepIndex + 1,
                    StepDescription = step,
                    Result = stepResult.Content ?? string.Empty,
                    ToolResults = stepToolResults
                };
                stepObservations.Add(observation);

                // 将步骤结果追加到对话历史
                messages.Add(new AgentMessage("assistant", stepResult.Content ?? string.Empty)
                {
                    ToolCalls = stepResult.ToolCalls
                });

                foreach (var tr in stepToolResults)
                {
                    messages.Add(new AgentMessage("tool", tr.Content ?? "工具未返回结果。", tr.ToolName, tr.ToolCallId));
                }
            }

            // === Phase 3: Synthesis ===
            var synthesisRequest = BuildSynthesisRequest(prepared, messages, stepObservations);
            var synthesisResponse = await context.LLMBackend.CompleteAsync(synthesisRequest, cancellationToken);
            totalIterations++;

            synthesisResponse.StopReason = "completed";
            synthesisResponse.Iterations = totalIterations;
            synthesisResponse.ConversationHistory = messages;
            synthesisResponse.ToolResults = allToolResults;
            return synthesisResponse;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<AgentStreamChunk> StreamAsync(
            IAgentLoopContext context,
            AgentRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var prepared = await context.PrepareRequestAsync(request, cancellationToken);
            var messages = new List<AgentMessage>(prepared.Messages);
            var totalIterations = 0;

            // === Phase 1: Planning（非流式，快速拿到计划） ===
            var planRequest = BuildPlanRequest(prepared, PlanPromptTemplate, StepCountRange);
            var planResponse = await context.LLMBackend.CompleteAsync(planRequest, cancellationToken);
            totalIterations++;

            var plan = ParsePlan(planResponse.Content);

            // 发送计划片段
            if (EmitPlanInStream)
            {
                yield return new AgentStreamChunk
                {
                    Type = "plan",
                    Iteration = totalIterations,
                    ContentDelta = planResponse.Content ?? string.Empty
                };
            }

            if (plan.Steps is null || plan.Steps.Count == 0)
            {
                // 无计划 → 直接流式返回 LLM 回复
                messages.Add(new AgentMessage("assistant", planResponse.Content ?? string.Empty));

                yield return new AgentStreamChunk
                {
                    Type = "iteration_end",
                    Iteration = totalIterations,
                    StopReason = "no_plan"
                };
                yield break;
            }

            if (plan.Steps.Count > MaxPlanSteps)
            {
                plan.Steps = plan.Steps.Take(MaxPlanSteps).ToList();
            }

            messages.Add(new AgentMessage("assistant", planResponse.Content ?? string.Empty));

            // === Phase 2: Execution（逐步执行，每步内含工具循环） ===
            var allToolResults = new List<ToolResult>();
            var stepObservations = new List<StepObservation>();

            for (var stepIndex = 0; stepIndex < plan.Steps.Count; stepIndex++)
            {
                var step = plan.Steps[stepIndex];
                var stepMessages = new List<AgentMessage>(messages);
                var stepRequest = BuildStepRequest(prepared, stepMessages, step, stepIndex, plan.Steps.Count);

                // 发送步骤开始标记
                yield return new AgentStreamChunk
                {
                    Type = "step_start",
                    Iteration = totalIterations + 1,
                    ContentDelta = $"[Step {stepIndex + 1}/{plan.Steps.Count}] {step}"
                };

                // 流式执行步骤（含工具循环，与非流式路径行为一致）
                var stepToolResults = new List<ToolResult>();
                var stepContent = string.Empty;

                for (var subIter = 0; subIter < MaxIterationsPerStep; subIter++)
                {
                    stepRequest.Messages = stepMessages;
                    var toolCallBuffers = new Dictionary<int, StreamToolCallBuffer>();

                    await foreach (var chunk in context.LLMBackend.StreamAsync(stepRequest, cancellationToken).WithCancellation(cancellationToken))
                    {
                        chunk.Iteration = totalIterations + 1;
                        yield return chunk;

                        if (chunk.ToolCallDelta is not null)
                        {
                            StreamToolCallHelper.MergeToolCallDelta(toolCallBuffers, chunk.ToolCallDelta);
                        }

                        stepContent += chunk.ContentDelta ?? string.Empty;
                    }

                    totalIterations++;

                    // 无工具调用 → 步骤完成
                    if (toolCallBuffers.Count == 0 || !stepRequest.EnableToolDispatch)
                    {
                        break;
                    }

                    // 有工具调用 → 执行工具，追加消息，继续循环
                    var completedToolCalls = StreamToolCallHelper.BuildCompletedToolCalls(toolCallBuffers);
                    var toolResults = await context.ExecuteToolsAsync(completedToolCalls, cancellationToken);
                    stepToolResults.AddRange(toolResults);
                    allToolResults.AddRange(toolResults);

                    yield return new AgentStreamChunk
                    {
                        Type = "tool_results",
                        Iteration = totalIterations,
                        ToolCalls = completedToolCalls,
                        ToolResults = toolResults,
                        FinishReason = "tool_calls"
                    };

                    // 追加 assistant 消息
                    stepMessages.Add(new AgentMessage("assistant", stepContent) { ToolCalls = completedToolCalls });
                    // 追加 tool result 消息
                    foreach (var tc in completedToolCalls)
                    {
                        var result = toolResults.FirstOrDefault(r => r.ToolCallId == tc.Id);
                        stepMessages.Add(new AgentMessage("tool", result?.Content ?? "工具未返回结果。", tc.Function?.Name, tc.Id));
                    }

                    // 最后一次子迭代禁用工具，确保步骤能终止
                    if (subIter == MaxIterationsPerStep - 2)
                    {
                        stepRequest.EnableToolDispatch = false;
                    }
                }

                // 记录观察结果
                stepObservations.Add(new StepObservation
                {
                    StepIndex = stepIndex + 1,
                    StepDescription = step,
                    Result = stepContent,
                    ToolResults = stepToolResults
                });

                // 同步到全局消息历史
                messages.AddRange(stepMessages.Skip(messages.Count));
            }

            // === Phase 3: Synthesis（流式输出最终综合回答） ===
            var synthesisRequest = BuildSynthesisRequest(prepared, messages, stepObservations);
            totalIterations++;

            await foreach (var chunk in context.LLMBackend.StreamAsync(synthesisRequest, cancellationToken).WithCancellation(cancellationToken))
            {
                chunk.Iteration = totalIterations;
                yield return chunk;
            }

            yield return new AgentStreamChunk
            {
                Type = "iteration_end",
                Iteration = totalIterations,
                StopReason = "completed"
            };
        }

        #region 请求构建

        /// <summary>
        /// 构建 Planning 阶段请求：引导 LLM 输出结构化的步骤列表。
        /// </summary>
        private static AgentRequest BuildPlanRequest(AgentRequest prepared, string planPromptTemplate, string stepCountRange)
        {
            var planPrompt = string.Format(planPromptTemplate, stepCountRange);
            return new AgentRequest
            {
                Model = prepared.Model,
                SystemPrompt = MergeSystemPrompt(planPrompt, prepared.SystemPrompt),
                Messages = prepared.Messages,
                Temperature = 0.3m, // 低温度，保证计划结构化
                MaxTokens = prepared.MaxTokens,
                EnableToolDispatch = false, // 计划阶段不调用工具
                Tools = new List<ToolDefinition>() // 不传工具定义
            };
        }

        /// <summary>
        /// 构建 Execution 阶段单步骤请求。
        /// </summary>
        private AgentRequest BuildStepRequest(
            AgentRequest prepared,
            List<AgentMessage> messages,
            string stepDescription,
            int stepIndex,
            int totalSteps)
        {
            var stepPrompt = string.Format(StepPromptTemplate, stepIndex + 1, totalSteps, stepDescription);
            return new AgentRequest
            {
                Model = prepared.Model,
                SystemPrompt = MergeSystemPrompt(stepPrompt, prepared.SystemPrompt),
                Messages = messages,
                Temperature = prepared.Temperature,
                MaxTokens = prepared.MaxTokens,
                EnableToolDispatch = prepared.EnableToolDispatch,
                Tools = prepared.Tools
            };
        }

        /// <summary>
        /// 构建 Synthesis 阶段请求：综合所有步骤结果生成最终回答。
        /// 将步骤观察摘要注入系统提示词，帮助 LLM 理解全局执行结果。
        /// </summary>
        private AgentRequest BuildSynthesisRequest(
            AgentRequest prepared,
            List<AgentMessage> messages,
            List<StepObservation> observations)
        {
            var observationSummary = new System.Text.StringBuilder();
            observationSummary.AppendLine("以下是之前执行的所有步骤及其结果：\n");
            for (var i = 0; i < observations.Count; i++)
            {
                var obs = observations[i];
                observationSummary.AppendLine($"## 步骤 {obs.StepIndex}：{obs.StepDescription}");
                observationSummary.AppendLine(obs.Result);
                if (obs.ToolResults is { Count: > 0 })
                {
                    foreach (var tr in obs.ToolResults)
                    {
                        observationSummary.AppendLine($"  [工具 {tr.ToolName}] {(tr.Success ? "成功" : "失败")}: {tr.Content}");
                    }
                }
                observationSummary.AppendLine();
            }

            var synthesisPrompt = string.Format(SynthesisPromptTemplate, observationSummary);
            return new AgentRequest
            {
                Model = prepared.Model,
                SystemPrompt = MergeSystemPrompt(synthesisPrompt, prepared.SystemPrompt),
                Messages = messages,
                Temperature = prepared.Temperature,
                MaxTokens = prepared.MaxTokens,
                EnableToolDispatch = false, // 综合阶段不再调用工具
                Tools = new List<ToolDefinition>()
            };
        }

        private static string MergeSystemPrompt(string primary, string secondary)
        {
            if (string.IsNullOrWhiteSpace(primary)) return secondary;
            if (string.IsNullOrWhiteSpace(secondary)) return primary;
            return $"{primary}\n\n{secondary}";
        }

        #endregion

        #region 计划解析

        /// <summary>
        /// 解析 LLM 返回的计划 JSON，提取步骤列表。
        /// 容错处理：如果 JSON 解析失败，尝试提取行列表。
        /// </summary>
        private static PlanResult ParsePlan(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new PlanResult();
            }

            // 尝试提取 JSON 块
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content[jsonStart..(jsonEnd + 1)];
                try
                {
                    var result = JsonSerializer.Deserialize<PlanResult>(json, JsonOptions);
                    if (result?.Steps is { Count: > 0 })
                    {
                        return result;
                    }
                }
                catch (JsonException)
                {
                    // JSON 解析失败，回退到行列表解析
                }
            }

            // 回退：按行解析为步骤
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                               .Select(l => l.TrimStart(' ', '-', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.'))
                               .Where(l => !string.IsNullOrWhiteSpace(l))
                               .ToList();

            return lines.Count > 0 ? new PlanResult { Steps = lines } : new PlanResult();
        }

        /// <summary>
        /// 计划解析结果。
        /// </summary>
        private sealed class PlanResult
        {
            [JsonPropertyName("steps")]
            public List<string> Steps { get; set; } = new();
        }

        #endregion

        #region 步骤执行（含工具循环）

        /// <summary>
        /// 执行单个步骤，内部包含类似 ReAct 的工具调用循环。
        /// </summary>
        private async Task<(AgentResponse Response, List<ToolResult> ToolResults, int Iterations)> ExecuteStepWithToolsAsync(
            IAgentLoopContext context,
            AgentRequest stepRequest,
            List<AgentMessage> messages,
            CancellationToken cancellationToken)
        {
            var allToolResults = new List<ToolResult>();
            var iteration = 0;

            for (var i = 0; i < MaxIterationsPerStep; i++)
            {
                iteration++;
                stepRequest.Messages = messages;

                var response = await context.LLMBackend.CompleteAsync(stepRequest, cancellationToken);

                if (!context.ShouldExecuteTools(stepRequest, response))
                {
                    return (response, allToolResults, iteration);
                }

                // 执行工具
                var toolResults = await context.ExecuteToolsAsync(response.ToolCalls, cancellationToken);
                allToolResults.AddRange(toolResults);

                // 追加消息
                messages.Add(new AgentMessage("assistant", response.Content ?? string.Empty)
                {
                    ToolCalls = response.ToolCalls
                });

                foreach (var toolCall in response.ToolCalls)
                {
                    var result = toolResults.FirstOrDefault(r => r.ToolCallId == toolCall.Id);
                    messages.Add(new AgentMessage("tool", result?.Content ?? "工具未返回结果。", toolCall.Function?.Name, toolCall.Id));
                }

                // 最后一次迭代前禁用工具，确保步骤能终止
                if (i == MaxIterationsPerStep - 2)
                {
                    stepRequest.EnableToolDispatch = false;
                }
            }

            // 步骤内迭代耗尽，做一次无工具调用
            stepRequest.Messages = messages;
            stepRequest.EnableToolDispatch = false;
            var finalResponse = await context.LLMBackend.CompleteAsync(stepRequest, cancellationToken);
            return (finalResponse, allToolResults, iteration);
        }

        #endregion

        #region 内部数据模型

        /// <summary>
        /// 步骤执行观察结果，用于 Synthesis 阶段综合。
        /// </summary>
        private sealed class StepObservation
        {
            /// <summary>步骤序号（从 1 开始）。</summary>
            public int StepIndex { get; set; }
            /// <summary>步骤描述。</summary>
            public string StepDescription { get; set; }
            /// <summary>步骤执行结果文本。</summary>
            public string Result { get; set; }
            /// <summary>该步骤中的工具调用结果。</summary>
            public List<ToolResult> ToolResults { get; set; }
        }

        #endregion
    }
}
