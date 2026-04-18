using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;

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

    /// <summary>
    /// 初始化 LLM 服务。
    /// </summary>
    public LLMService(ILLMProvider provider, ILLMToolDispatcher toolDispatcher, ILLMSkillRegistry skillRegistry)
    {
        _provider = provider;
        _toolDispatcher = toolDispatcher;
        _skillRegistry = skillRegistry;
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

        for (var i = 1; i <= maxIterations; i++)
        {
            iteration = i;
            preparedRequest.Messages = messages;

            var response = await _provider.ChatAsync(preparedRequest, cancellationToken);

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
        preparedRequest.Messages = messages;
        preparedRequest.EnableAutoToolDispatch = false;
        preparedRequest.ToolChoice = new LLMToolChoice { Type = "none" };

        var finalResponse = await _provider.ChatAsync(preparedRequest, cancellationToken);
        finalResponse.StopReason = stopReason;
        finalResponse.Iterations = iteration;
        finalResponse.ConversationHistory = messages;
        finalResponse.ToolResults = allToolResults;
        return finalResponse;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LLMStreamEvent> StreamAsync(LLMChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var preparedRequest = PrepareRequest(request);
        var messages = preparedRequest.Messages.Select(CloneMessage).ToList();
        var maxIterations = preparedRequest.MaxIterations <= 0 ? 1 : preparedRequest.MaxIterations;

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

        var skill = _skillRegistry.GetByName(request.SkillName);
        if (skill is null)
        {
            return preparedRequest;
        }

        preparedRequest.SystemPrompt = string.IsNullOrWhiteSpace(request.SkillOverridePrompt)
            ? MergeSystemPrompt(skill.SystemPrompt, request.SystemPrompt)
            : MergeSystemPrompt(request.SkillOverridePrompt, request.SystemPrompt);

        foreach (var tool in skill.DefaultTools)
        {
            if (!preparedRequest.Tools.Any(existing => string.Equals(existing.Function.Name, tool.Function.Name, StringComparison.OrdinalIgnoreCase)))
            {
                preparedRequest.Tools.Add(CloneToolDefinition(tool));
            }
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



    private sealed class StreamToolCallBuffer
    {
        public string Id { get; set; }

        public string Type { get; set; }

        public string Name { get; set; }

        public string Arguments { get; set; }
    }
}
