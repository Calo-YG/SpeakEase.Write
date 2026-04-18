using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// Agent Loop LLM 服务实现，负责技能注入与自动工具循环。
/// 参考 nanobot Agent Loop 模式：LLM 作为决策核心，在每轮迭代中决定是继续调用工具还是输出最终答案，
/// 实现真正的"思考-行动-观察"闭环（ReAct 模式），直到任务完成或达到最大迭代次数。
/// </summary>
public sealed class LLMService : ILLMService
{
    private readonly ILLMProvider _provider;
    private readonly ILLMToolDispatcher _toolDispatcher;
    private readonly ILLMSkillRegistry _skillRegistry;
    private readonly ILogger<LLMService> _logger;

    /// <summary>
    /// 初始化 LLM 服务。
    /// </summary>
    public LLMService(ILLMProvider provider, ILLMToolDispatcher toolDispatcher, ILLMSkillRegistry skillRegistry, ILogger<LLMService> logger)
    {
        _provider = provider;
        _toolDispatcher = toolDispatcher;
        _skillRegistry = skillRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LLMChatResponse> ChatAsync(LLMChatRequest request, CancellationToken cancellationToken = default)
    {
        var preparedRequest = PrepareRequest(request);
        var messages = preparedRequest.Messages.Select(CloneMessage).ToList();
        var allToolResults = new List<LLMToolExecutionResult>();
        var maxIterations = preparedRequest.MaxIterations <= 0 ? 1 : preparedRequest.MaxIterations;
        var iteration = 0;
        var stopReason = "completed";

        _logger.LogInformation("ChatAsync 开始: Skill={Skill}, Model={Model}, MaxIterations={MaxIter}, Messages={MsgCount}, Tools=[{Tools}]",
            preparedRequest.SkillName ?? "(auto)", preparedRequest.Model ?? "(default)", maxIterations, messages.Count,
            string.Join(", ", preparedRequest.Tools.Select(t => t.Function.Name)));

        for (var i = 1; i <= maxIterations; i++)
        {
            iteration = i;
            preparedRequest.Messages = messages;

            var response = await _provider.ChatAsync(preparedRequest, cancellationToken);

            var toolCallNames = response.ToolCalls?.Select(tc => $"{tc.Function.Name}({Truncate(tc.Function.Arguments, 80)})").ToList() ?? new List<string>();
            _logger.LogInformation("ChatAsync 迭代 {Iter}/{MaxIter}: FinishReason={FinishReason}, ToolCalls=[{ToolCallNames}], ContentLen={ContentLen}",
                i, maxIterations, response.FinishReason ?? "(null)", string.Join(", ", toolCallNames), response.Content?.Length ?? 0);


            // 安全门控：判断是否应该执行工具
            if (!ShouldExecuteTools(preparedRequest, response))
            {
                // 无工具调用 → 正常完成
                response.StopReason = stopReason;
                response.Iterations = iteration;
                response.ConversationHistory = messages;
                response.ToolResults = allToolResults;
                return response;
            }

            // 执行工具
            var toolResults = await _toolDispatcher.DispatchAsync(response.ToolCalls, cancellationToken);
            allToolResults.AddRange(toolResults);

            _logger.LogInformation("ChatAsync 工具执行完成: ToolNames=[{ToolNames}], Results=[{Results}]",
                string.Join(", ", response.ToolCalls.Select(tc => tc.Function.Name)),
                string.Join(", ", toolResults.Select(r => $"{r.ToolName}={(r.Success ? "ok" : r.ErrorCode)}")));


            // 追加 assistant 消息（含 tool_calls）
            messages.Add(new LLMChatMessage(
                "assistant",
                response.Content,
                null,
                null,
                response.ToolCalls.Select(CloneToolCall).ToList()));

            // 追加 tool result 消息
            foreach (var toolCall in response.ToolCalls)
            {
                var toolResult = toolResults.FirstOrDefault(r => string.Equals(r.ToolCallId, toolCall.Id, StringComparison.OrdinalIgnoreCase));
                var toolContent = toolResult?.Content ?? "工具未返回结果。";
                messages.Add(new LLMChatMessage(
                    "tool",
                    toolContent,
                    toolCall.Function.Name,
                    toolCall.Id));
            }
        }

        // 循环耗尽 → 达到最大迭代次数，再做一次无工具调用来获取最终回复
        stopReason = "max_iterations";
        _logger.LogWarning("ChatAsync 达到最大迭代次数 {MaxIter}，执行最终无工具调用", maxIterations);
        preparedRequest.Messages = messages;
        preparedRequest.EnableAutoToolDispatch = false;
        preparedRequest.ToolChoice = new LLMToolChoice { Type = "none" };

        var finalResponse = await _provider.ChatAsync(preparedRequest, cancellationToken);
        finalResponse.StopReason = stopReason;
        finalResponse.Iterations = iteration;
        finalResponse.ConversationHistory = messages;
        finalResponse.ToolResults = allToolResults;

        var calledToolNames = allToolResults.Select(r => r.ToolName).Distinct().ToList();
        _logger.LogInformation("ChatAsync 完成: Iterations={Iterations}, StopReason={StopReason}, Model={Model}, CalledTools=[{CalledTools}], Skill={Skill}",
            iteration, stopReason, finalResponse.FinalModel ?? finalResponse.Model, string.Join(", ", calledToolNames), preparedRequest.SkillName ?? "(auto)");


        return finalResponse;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LLMStreamEvent> StreamAsync(LLMChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var preparedRequest = PrepareRequest(request);
        var messages = preparedRequest.Messages.Select(CloneMessage).ToList();
        var maxIterations = preparedRequest.MaxIterations <= 0 ? 1 : preparedRequest.MaxIterations;

        _logger.LogInformation("StreamAsync 开始: Skill={Skill}, Model={Model}, MaxIterations={MaxIter}, Messages={MsgCount}, Tools=[{Tools}]",
            preparedRequest.SkillName ?? "(auto)", preparedRequest.Model ?? "(default)", maxIterations, messages.Count,
            string.Join(", ", preparedRequest.Tools.Select(t => t.Function.Name)));


        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            preparedRequest.Messages = messages;
            var toolCalls = new Dictionary<int, StreamToolCallBuffer>();
            string finishReason = null;

            await foreach (var streamEvent in _provider.StreamAsync(preparedRequest, cancellationToken).WithCancellation(cancellationToken))
            {
                if (streamEvent.ToolCallDelta is not null)
                {
                    MergeToolCallDelta(toolCalls, streamEvent.ToolCallDelta);
                }

                if (!string.IsNullOrWhiteSpace(streamEvent.FinishReason))
                {
                    finishReason = streamEvent.FinishReason;
                }

                streamEvent.Iteration = iteration;
                yield return streamEvent;
            }

            // 安全门控：判断是否应该执行工具
            var hasToolCalls = toolCalls.Count > 0;
            var shouldExecute = preparedRequest.EnableAutoToolDispatch
                && hasToolCalls
                && ShouldExecuteToolsByFinishReason(finishReason);

            if (!shouldExecute)
            {
                // 无工具调用 → 正常完成
                if (iteration == maxIterations && hasToolCalls)
                {
                    yield return new LLMStreamEvent
                    {
                        Type = "iteration_end",
                        Iteration = iteration,
                        StopReason = "max_iterations",
                        FinishReason = finishReason
                    };
                }
                else
                {
                    yield return new LLMStreamEvent
                    {
                        Type = "iteration_end",
                        Iteration = iteration,
                        StopReason = "completed",
                        FinishReason = finishReason
                    };
                }

                yield break;
            }

            // 执行工具
            var completedToolCalls = BuildCompletedToolCalls(toolCalls);
            var toolResults = await _toolDispatcher.DispatchAsync(completedToolCalls, cancellationToken);

            _logger.LogInformation("StreamAsync 迭代 {Iter}/{MaxIter} 工具执行完成: ToolNames=[{ToolNames}], Results=[{Results}]",
                iteration, maxIterations, string.Join(", ", completedToolCalls.Select(tc => tc.Function.Name)),
                string.Join(", ", toolResults.Select(r => $"{r.ToolName}={(r.Success ? "ok" : r.ErrorCode)}")));


            yield return new LLMStreamEvent
            {
                Type = "tool_results",
                Iteration = iteration,
                ToolCalls = completedToolCalls,
                ToolResults = toolResults.ToList(),
                FinishReason = "tool_calls"
            };

            // 追加 assistant 消息（含 tool_calls）
            // 流式模式下 assistant 内容已通过 content delta 发送，这里追加空内容的 assistant 消息携带 tool_calls
            messages.Add(new LLMChatMessage(
                "assistant",
                string.Empty,
                null,
                null,
                completedToolCalls.Select(CloneToolCall).ToList()));

            // 追加 tool result 消息
            foreach (var toolCall in completedToolCalls)
            {
                var toolResult = toolResults.FirstOrDefault(r => string.Equals(r.ToolCallId, toolCall.Id, StringComparison.OrdinalIgnoreCase));
                var toolContent = toolResult?.Content ?? "工具未返回结果。";
                messages.Add(new LLMChatMessage(
                    "tool",
                    toolContent,
                    toolCall.Function.Name,
                    toolCall.Id));
            }

            // 达到最大迭代次数，下一轮禁用工具调用
            if (iteration == maxIterations - 1)
            {
                preparedRequest.EnableAutoToolDispatch = false;
                preparedRequest.ToolChoice = new LLMToolChoice { Type = "none" };
            }
        }
    }

    /// <summary>
    /// 判断是否应该执行工具调用（安全门控）。
    /// 仅当 EnableAutoToolDispatch=true、有 tool_calls、且 finish_reason 为 tool_calls 或 stop 时才执行，
    /// 防止在内容审查拒绝(refusal/content_filter)等异常情况下仍执行工具。
    /// </summary>
    private static bool ShouldExecuteTools(LLMChatRequest request, LLMChatResponse response)
    {
        if (!request.EnableAutoToolDispatch)
        {
            return false;
        }

        if (response.ToolCalls is null || response.ToolCalls.Count == 0)
        {
            return false;
        }

        return ShouldExecuteToolsByFinishReason(response.FinishReason);
    }

    /// <summary>
    /// 根据 finish_reason 判断是否应执行工具。
    /// </summary>
    private static bool ShouldExecuteToolsByFinishReason(string finishReason)
    {
        if (string.IsNullOrWhiteSpace(finishReason))
        {
            return false;
        }

        return string.Equals(finishReason, "tool_calls", StringComparison.OrdinalIgnoreCase)
            || string.Equals(finishReason, "stop", StringComparison.OrdinalIgnoreCase);
    }

    private LLMChatRequest PrepareRequest(LLMChatRequest request)
    {
        var preparedRequest = new LLMChatRequest
        {
            Model = request.Model,
            FallbackModels = new List<string>(request.FallbackModels),
            SystemPrompt = request.SystemPrompt,
            Messages = request.Messages.Select(CloneMessage).ToList(),
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            UseJsonMode = request.UseJsonMode,
            Tools = request.Tools.Select(CloneToolDefinition).ToList(),
            ToolChoice = request.ToolChoice is null ? null : new LLMToolChoice
            {
                Type = request.ToolChoice.Type,
                Function = request.ToolChoice.Function is null ? null : new LLMToolChoiceFunction
                {
                    Name = request.ToolChoice.Function.Name
                }
            },
            EnableAutoToolDispatch = request.EnableAutoToolDispatch,
            MaxIterations = request.MaxIterations,
            SkillName = request.SkillName,
            SkillOverridePrompt = request.SkillOverridePrompt
        };

        // 1. 显式指定技能 → 精确匹配注入
        if (!string.IsNullOrWhiteSpace(request.SkillName))
        {
            var skill = _skillRegistry.GetByName(request.SkillName);
            if (skill is not null)
            {
                _logger.LogInformation("PrepareRequest: 匹配技能 SkillName={SkillName}, SystemPromptLen={PromptLen}, DefaultTools=[{DefaultTools}]",
                    skill.Name, skill.SystemPrompt?.Length ?? 0,
                    skill.DefaultTools is { Count: > 0 } ? string.Join(", ", skill.DefaultTools.Select(t => t.Function.Name)) : "(none)");


                preparedRequest.SystemPrompt = string.IsNullOrWhiteSpace(request.SkillOverridePrompt)
                    ? MergeSystemPrompt(skill.SystemPrompt, request.SystemPrompt)
                    : MergeSystemPrompt(request.SkillOverridePrompt, request.SystemPrompt);

                // 从 Dispatcher 获取完整工具定义（含 parameters JSON Schema）
                foreach (var tool in skill.DefaultTools)
                {
                    AddToolDefinitionFromDispatcher(preparedRequest, tool.Function.Name);
                }

                return preparedRequest;
            }

            _logger.LogDebug("PrepareRequest: 未匹配技能 SkillName={SkillName}", request.SkillName);
        }

        // 2. 未指定技能 → 自动路由：注入技能目录 + 全量工具，由 LLM 自主分析语义选用
        var allSkills = _skillRegistry.GetAll();
        if (allSkills is { Count: > 0 })
        {
            var autoRouterPrompt = BuildAutoRouterPrompt(allSkills);
            preparedRequest.SystemPrompt = MergeSystemPrompt(autoRouterPrompt, request.SystemPrompt);

            // 合并所有技能的默认工具（去重），从 Dispatcher 获取完整定义
            var addedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var skill in allSkills)
            {
                foreach (var tool in skill.DefaultTools)
                {
                    if (addedToolNames.Add(tool.Function.Name))
                    {
                        AddToolDefinitionFromDispatcher(preparedRequest, tool.Function.Name);
                    }
                }
            }

            _logger.LogInformation("PrepareRequest: 自动路由模式，注入 Skills=[{SkillNames}], Tools=[{ToolNames}]",
                string.Join(", ", allSkills.Select(s => s.Name)),
                string.Join(", ", preparedRequest.Tools.Select(t => t.Function.Name)));
        }

        return preparedRequest;
    }

    private static LLMChatMessage CloneMessage(LLMChatMessage message)
    {
        return new LLMChatMessage(
            message.Role,
            message.Content,
            message.Name,
            message.ToolCallId,
            message.ToolCalls?.Select(CloneToolCall).ToList());
    }

    private static string MergeSystemPrompt(string primary, string secondary)
    {
        if (string.IsNullOrWhiteSpace(primary))
        {
            return secondary;
        }

        if (string.IsNullOrWhiteSpace(secondary))
        {
            return primary;
        }

        return $"{primary}\n\n{secondary}";
    }

    /// <summary>
    /// 构建自动路由系统提示词：列出所有技能名称+描述+行为指引，
    /// 指导 LLM 分析用户语义后自主选择最匹配的技能模式。
    /// </summary>
    private static string BuildAutoRouterPrompt(IReadOnlyList<LLMSkillDefinition> skills)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 AINW 智能助手，具备以下专业技能。请根据用户输入的语义自动选择最匹配的技能模式来回答。");
        sb.AppendLine();
        sb.AppendLine("## 可用技能");
        sb.AppendLine();

        for (var i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            sb.AppendLine($"### {i + 1}. {skill.Name}");
            if (!string.IsNullOrWhiteSpace(skill.Description))
            {
                sb.AppendLine($"描述: {skill.Description}");
            }
            if (!string.IsNullOrWhiteSpace(skill.SystemPrompt))
            {
                sb.AppendLine($"行为指引: {skill.SystemPrompt}");
            }
            if (skill.DefaultTools is { Count: > 0 })
            {
                sb.AppendLine($"默认工具: {string.Join(", ", skill.DefaultTools.Select(t => t.Function.Name))}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 指令");
        sb.AppendLine("分析用户输入的语义，自动采用最匹配的技能行为模式来回答。如果用户请求跨越多个技能领域，综合运用相关技能。回答时不需要声明使用了哪个技能，直接以该技能的专业风格作答即可。");

        return sb.ToString();
    }

    private static LLMToolDefinition CloneToolDefinition(LLMToolDefinition source)
    {
        return new LLMToolDefinition
        {
            Type = source.Type,
            Function = new LLMToolFunctionDefinition
            {
                Name = source.Function.Name,
                Description = source.Function.Description,
                Parameters = source.Function.Parameters
            }
        };
    }

    /// <summary>
    /// 从 Dispatcher 获取工具的完整定义并添加到请求中（如果尚未存在）。
    /// 优先使用 Handler 自带的 ToolDefinition（含 parameters JSON Schema），回退到精简定义。
    /// </summary>
    private void AddToolDefinitionFromDispatcher(LLMChatRequest preparedRequest, string toolName)
    {
        if (preparedRequest.Tools.Any(existing => string.Equals(existing.Function.Name, toolName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        // 优先从 Dispatcher 获取完整定义
        if (_toolDispatcher is LLMToolDispatcher dispatcher)
        {
            var fullDef = dispatcher.GetToolDefinition(toolName);
            if (fullDef is not null)
            {
                preparedRequest.Tools.Add(CloneToolDefinition(fullDef));
                return;
            }
        }

        // 回退：添加精简定义（无 parameters）
        preparedRequest.Tools.Add(new LLMToolDefinition
        {
            Type = "function",
            Function = new LLMToolFunctionDefinition
            {
                Name = toolName,
                Description = $"工具: {toolName}"
            }
        });
    }

    private static LLMToolCall CloneToolCall(LLMToolCall source)
    {
        return new LLMToolCall
        {
            Id = source.Id,
            Type = source.Type,
            Function = new LLMToolFunctionCall
            {
                Name = source.Function.Name,
                Arguments = source.Function.Arguments
            }
        };
    }

    private static void MergeToolCallDelta(IDictionary<int, StreamToolCallBuffer> bufferMap, LLMToolCallDelta delta)
    {
        if (!bufferMap.TryGetValue(delta.Index, out var buffer))
        {
            buffer = new StreamToolCallBuffer();
            bufferMap[delta.Index] = buffer;
        }

        if (!string.IsNullOrWhiteSpace(delta.Id))
        {
            buffer.Id = delta.Id;
        }

        if (!string.IsNullOrWhiteSpace(delta.Type))
        {
            buffer.Type = delta.Type;
        }

        if (!string.IsNullOrWhiteSpace(delta.Name))
        {
            buffer.Name ??= string.Empty;
            buffer.Name += delta.Name;
        }

        if (!string.IsNullOrWhiteSpace(delta.Arguments))
        {
            buffer.Arguments ??= string.Empty;
            buffer.Arguments += delta.Arguments;
        }
    }

    private static List<LLMToolCall> BuildCompletedToolCalls(IDictionary<int, StreamToolCallBuffer> bufferMap)
    {
        return bufferMap
            .OrderBy(pair => pair.Key)
            .Select(pair => new LLMToolCall
            {
                Id = pair.Value.Id,
                Type = string.IsNullOrWhiteSpace(pair.Value.Type) ? "function" : pair.Value.Type,
                Function = new LLMToolFunctionCall
                {
                    Name = pair.Value.Name ?? string.Empty,
                    Arguments = pair.Value.Arguments ?? string.Empty
                }
            })
            .ToList();
    }



    /// <summary>
    /// 截断字符串，用于日志输出时避免过长。
    /// </summary>
    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    private sealed class StreamToolCallBuffer
    {
        public string Id { get; set; }

        public string Type { get; set; }

        public string Name { get; set; }

        public string Arguments { get; set; }
    }
}
