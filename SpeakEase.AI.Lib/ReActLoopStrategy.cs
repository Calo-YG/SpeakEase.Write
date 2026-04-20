using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// ReAct（Reasoning-Action-Observation）Loop 策略实现。
    /// 
    /// 循环流程：
    /// 1. PrepareRequest → 注入技能/工具到请求
    /// 2. 调用 IAgentLLMBackend 获取 LLM 响应
    /// 3. 若无工具调用 → 返回最终响应
    /// 4. 若有工具调用 → 执行工具 → 追加结果到消息 → 回到步骤 2
    /// 5. 达到最大迭代次数 → 做一次无工具调用获取最终回复
    /// </summary>
    public class ReActLoopStrategy : IReActStrategy
    {
        /// <summary>
        /// 默认最大迭代次数。
        /// </summary>
        public int DefaultMaxIterations { get; set; } = 20;

        /// <inheritdoc />
        public async Task<AgentResponse> ExecuteAsync(
            IAgentLoopContext context,
            AgentRequest request,
            CancellationToken cancellationToken)
        {
            var prepared = await context.PrepareRequestAsync(request, cancellationToken);
            var messages = new List<AgentMessage>(prepared.Messages);
            var maxIterations = prepared.MaxIterations ?? DefaultMaxIterations;
            if (maxIterations <= 0) maxIterations = 1;
            var allToolResults = new List<ToolResult>();
            var iteration = 0;
            var stopReason = "completed";

            for (var i = 1; i <= maxIterations; i++)
            {
                iteration = i;
                prepared.Messages = messages;

                var response = await context.LLMBackend.CompleteAsync(prepared, cancellationToken);

                if (!context.ShouldExecuteTools(prepared, response))
                {
                    response.StopReason = stopReason;
                    response.Iterations = iteration;
                    response.ConversationHistory = messages;
                    response.ToolResults = allToolResults;
                    return response;
                }

                // 执行工具
                var toolResults = await context.ExecuteToolsAsync(response.ToolCalls, cancellationToken);
                allToolResults.AddRange(toolResults);

                // 追加 assistant 消息（含 tool_calls）
                messages.Add(new AgentMessage("assistant", response.Content ?? string.Empty)
                {
                    ToolCalls = response.ToolCalls
                });

                // 追加 tool result 消息
                foreach (var toolCall in response.ToolCalls)
                {
                    var result = toolResults.FirstOrDefault(r => r.ToolCallId == toolCall.Id);
                    messages.Add(new AgentMessage("tool", result?.Content ?? "工具未返回结果。", toolCall.Function?.Name, toolCall.Id));
                }
            }

            // 循环耗尽 → 最终无工具调用
            stopReason = "max_iterations";
            prepared.Messages = messages;
            prepared.EnableToolDispatch = false;

            var finalResponse = await context.LLMBackend.CompleteAsync(prepared, cancellationToken);
            finalResponse.StopReason = stopReason;
            finalResponse.Iterations = iteration;
            finalResponse.ConversationHistory = messages;
            finalResponse.ToolResults = allToolResults;
            return finalResponse;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<AgentStreamChunk> StreamAsync(
            IAgentLoopContext context,
            AgentRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var prepared = await context.PrepareRequestAsync(request, cancellationToken);
            var messages = new List<AgentMessage>(prepared.Messages);
            var maxIterations = prepared.MaxIterations ?? DefaultMaxIterations;
            if (maxIterations <= 0) maxIterations = 1;

            for (var iteration = 1; iteration <= maxIterations; iteration++)
            {
                prepared.Messages = messages;
                var toolCallBuffers = new Dictionary<int, StreamToolCallBuffer>();
                string finishReason = null;

                await foreach (var chunk in context.LLMBackend.StreamAsync(prepared, cancellationToken).WithCancellation(cancellationToken))
                {
                    // 累积工具调用增量
                    if (chunk.ToolCallDelta is not null)
                    {
                        StreamToolCallHelper.MergeToolCallDelta(toolCallBuffers, chunk.ToolCallDelta);
                    }

                    if (!string.IsNullOrWhiteSpace(chunk.FinishReason))
                    {
                        finishReason = chunk.FinishReason;
                    }

                    chunk.Iteration = iteration;
                    yield return chunk;
                }

                // 判断是否应执行工具
                var hasToolCalls = toolCallBuffers.Count > 0;
                var shouldExecute = prepared.EnableToolDispatch && hasToolCalls;

                if (!shouldExecute)
                {
                    yield return new AgentStreamChunk
                    {
                        Type = "iteration_end",
                        Iteration = iteration,
                        StopReason = iteration == maxIterations && hasToolCalls ? "max_iterations" : "completed",
                        FinishReason = finishReason
                    };
                    yield break;
                }

                // 构建完成的工具调用并执行
                var completedToolCalls = StreamToolCallHelper.BuildCompletedToolCalls(toolCallBuffers);
                var toolResults = await context.ExecuteToolsAsync(completedToolCalls, cancellationToken);

                yield return new AgentStreamChunk
                {
                    Type = "tool_results",
                    Iteration = iteration,
                    ToolCalls = completedToolCalls,
                    ToolResults = toolResults,
                    FinishReason = "tool_calls"
                };

                // 追加 assistant 消息
                messages.Add(new AgentMessage("assistant", string.Empty)
                {
                    ToolCalls = completedToolCalls
                });

                // 追加 tool result 消息
                foreach (var toolCall in completedToolCalls)
                {
                    var result = toolResults.FirstOrDefault(r => r.ToolCallId == toolCall.Id);
                    messages.Add(new AgentMessage("tool", result?.Content ?? "工具未返回结果。", toolCall.Function?.Name, toolCall.Id));
                }

                // 下一轮是最后一轮时禁用工具
                if (iteration == maxIterations - 1)
                {
                    prepared.EnableToolDispatch = false;
                }
            }
        }
    }
}
