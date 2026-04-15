using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// 默认 LLM 服务实现，负责技能注入、自动工具分发和二轮补全。
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
        var firstResponse = await _provider.ChatAsync(preparedRequest, cancellationToken);

        if (!preparedRequest.EnableAutoToolDispatch || firstResponse.ToolCalls.Count == 0)
        {
            return firstResponse;
        }

        var toolResults = await _toolDispatcher.DispatchAsync(firstResponse.ToolCalls, cancellationToken);
        var secondRequest = BuildSecondRoundRequest(preparedRequest, firstResponse.Content, firstResponse.ToolCalls, toolResults);
        var secondResponse = await _provider.ChatAsync(secondRequest, cancellationToken);
        secondResponse.ToolResults = toolResults.ToList();
        return secondResponse;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LLMStreamEvent> StreamAsync(LLMChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var preparedRequest = PrepareRequest(request);
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

            yield return streamEvent;
        }

        if (!preparedRequest.EnableAutoToolDispatch || !string.Equals(finishReason, "tool_calls", StringComparison.OrdinalIgnoreCase) || toolCalls.Count == 0)
        {
            yield break;
        }

        var completedToolCalls = BuildCompletedToolCalls(toolCalls);
        var toolResults = await _toolDispatcher.DispatchAsync(completedToolCalls, cancellationToken);

        yield return new LLMStreamEvent
        {
            Type = "tool_results",
            ToolCalls = completedToolCalls,
            ToolResults = toolResults.ToList(),
            FinishReason = "tool_calls"
        };

        var secondRequest = BuildSecondRoundRequest(preparedRequest, string.Empty, completedToolCalls, toolResults);

        await foreach (var streamEvent in _provider.StreamAsync(secondRequest, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return streamEvent;
        }
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

    private static LLMChatRequest BuildSecondRoundRequest(
        LLMChatRequest originalRequest,
        string assistantContent,
        IReadOnlyList<LLMToolCall> toolCalls,
        IReadOnlyList<LLMToolExecutionResult> toolResults)
    {
        var secondRoundMessages = originalRequest.Messages.Select(CloneMessage).ToList();

        secondRoundMessages.Add(new LLMChatMessage(
            "assistant",
            assistantContent,
            null,
            null,
            toolCalls.Select(CloneToolCall).ToList()));

        foreach (var toolCall in toolCalls)
        {
            var toolResult = toolResults.FirstOrDefault(result => string.Equals(result.ToolCallId, toolCall.Id, StringComparison.OrdinalIgnoreCase));
            var toolContent = toolResult?.Content ?? "工具未返回结果。";
            secondRoundMessages.Add(new LLMChatMessage(
                "tool",
                toolContent,
                toolCall.Function.Name,
                toolCall.Id));
        }

        return new LLMChatRequest
        {
            Model = originalRequest.Model,
            FallbackModels = new List<string>(originalRequest.FallbackModels),
            SystemPrompt = originalRequest.SystemPrompt,
            Messages = secondRoundMessages,
            Temperature = originalRequest.Temperature,
            MaxTokens = originalRequest.MaxTokens,
            UseJsonMode = originalRequest.UseJsonMode,
            Tools = originalRequest.Tools.Select(CloneToolDefinition).ToList(),
            ToolChoice = new LLMToolChoice { Type = "none" },
            EnableAutoToolDispatch = false,
            SkillName = originalRequest.SkillName,
            SkillOverridePrompt = originalRequest.SkillOverridePrompt
        };
    }

    private sealed class StreamToolCallBuffer
    {
        public string Id { get; set; }

        public string Type { get; set; }

        public string Name { get; set; }

        public string Arguments { get; set; }
    }
}
