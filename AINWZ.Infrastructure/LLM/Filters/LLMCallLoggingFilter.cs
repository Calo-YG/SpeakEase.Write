using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using System.Text.Json;

namespace AINWZ.Infrastructure.LLM.Filters;

/// <summary>
/// AI 调用日志持久化切片。
/// </summary>
/// <remarks>
/// 初始化日志切片。
/// </remarks>
public sealed class LLMCallLoggingFilter(ILLMCallLogStore callLogStore) : ILLMServiceFilter
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<LLMChatResponse> InvokeChatAsync(
        LLMChatRequest request,
        Func<LLMChatRequest, CancellationToken, Task<LLMChatResponse>> next,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await next(request, cancellationToken);
            await TrySaveChatLogAsync(request, response, null, cancellationToken);
            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await TrySaveChatLogAsync(request, null, exception, cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LLMStreamEvent> InvokeStreamAsync(
        LLMChatRequest request,
        Func<LLMChatRequest, CancellationToken, IAsyncEnumerable<LLMStreamEvent>> next,
        CancellationToken cancellationToken = default)
    {
        return StreamWithLoggingAsync(request, next, cancellationToken);
    }

    private async IAsyncEnumerable<LLMStreamEvent> StreamWithLoggingAsync(
        LLMChatRequest request,
        Func<LLMChatRequest, CancellationToken, IAsyncEnumerable<LLMStreamEvent>> next,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var events = new List<LLMStreamEvent>();
        Exception exception = null;
        var enumerator = next(request, cancellationToken).GetAsyncEnumerator(cancellationToken);

        await using var asyncEnumerator = enumerator;

        while (true)
        {
            bool hasNext;

            try
            {
                hasNext = await asyncEnumerator.MoveNextAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                exception = ex;
                break;
            }

            if (!hasNext)
            {
                break;
            }

            var streamEvent = asyncEnumerator.Current;
            events.Add(CloneStreamEvent(streamEvent));
            yield return streamEvent;
        }

        await TrySaveStreamLogAsync(request, events, exception, cancellationToken);

        if (exception is not null)
        {
            throw exception;
        }
    }

    private async Task TrySaveChatLogAsync(LLMChatRequest request, LLMChatResponse response, Exception exception, CancellationToken cancellationToken)
    {
        var record = new LLMCallLogRecord
        {
            CallType = "chat",
            SkillName = request.SkillName,
            RequestSummary = BuildRequestSummary(request),
            ResponseSummary = response?.Content,
            PrimaryModel = response?.PrimaryModel ?? request.Model ?? request.FallbackModels.FirstOrDefault(),
            FinalModel = response?.FinalModel ?? response?.Model,
            UsedFallback = response?.UsedFallback ?? false,
            FallbackModel = response?.FallbackModel,
            RequestId = response?.RequestId,
            FinishReason = response?.FinishReason,
            ToolCallsSummary = SerializeSafely(response?.ToolCalls),
            ToolResultsSummary = SerializeSafely(response?.ToolResults),
            Success = exception is null,
            ErrorMessage = exception?.Message
        };

        await callLogStore.SaveAsync(record, cancellationToken);
    }

    private async Task TrySaveStreamLogAsync(LLMChatRequest request, IReadOnlyList<LLMStreamEvent> events, Exception exception, CancellationToken cancellationToken)
    {
        var lastDoneEvent = events.LastOrDefault(item => string.Equals(item.Type, "done", StringComparison.OrdinalIgnoreCase));
        var lastErrorEvent = events.LastOrDefault(item => string.Equals(item.Type, "error", StringComparison.OrdinalIgnoreCase));
        var toolResultEvent = events.LastOrDefault(item => string.Equals(item.Type, "tool_results", StringComparison.OrdinalIgnoreCase));
        var content = string.Concat(events.Where(item => string.Equals(item.Type, "chunk", StringComparison.OrdinalIgnoreCase)).Select(item => item.Content));

        var record = new LLMCallLogRecord
        {
            CallType = "stream",
            SkillName = request.SkillName,
            RequestSummary = BuildRequestSummary(request),
            ResponseSummary = content,
            PrimaryModel = request.Model ?? request.FallbackModels.FirstOrDefault(),
            FinalModel = lastDoneEvent?.Model ?? lastErrorEvent?.Model,
            UsedFallback = events.Any(item => item.UsedFallback),
            FallbackModel = lastDoneEvent?.ToModel ?? lastErrorEvent?.ToModel,
            RequestId = lastDoneEvent?.RequestId ?? lastErrorEvent?.RequestId,
            FinishReason = lastDoneEvent?.FinishReason ?? toolResultEvent?.FinishReason,
            ToolCallsSummary = SerializeSafely(toolResultEvent?.ToolCalls ?? lastDoneEvent?.ToolCalls),
            ToolResultsSummary = SerializeSafely(toolResultEvent?.ToolResults),
            Success = exception is null && lastErrorEvent is null,
            ErrorMessage = exception?.Message ?? lastErrorEvent?.ErrorMessage
        };

        await callLogStore.SaveAsync(record, cancellationToken);
    }

    private static string BuildRequestSummary(LLMChatRequest request)
    {
        var summary = new
        {
            request.SkillName,
            request.Model,
            request.FallbackModels,
            request.UseJsonMode,
            request.EnableAutoToolDispatch,
            messageCount = request.Messages.Count,
            tools = request.Tools.Select(tool => tool.Function.Name).ToList(),
            messages = request.Messages.Select(message => new
            {
                message.Role,
                Content = Truncate(message.Content, 500)
            }).ToList()
        };

        return JsonSerializer.Serialize(summary, JsonSerializerOptions);
    }

    private static string SerializeSafely<T>(T value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(value, JsonSerializerOptions);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private static LLMStreamEvent CloneStreamEvent(LLMStreamEvent streamEvent)
    {
        return new LLMStreamEvent
        {
            Type = streamEvent.Type,
            RequestId = streamEvent.RequestId,
            Model = streamEvent.Model,
            FromModel = streamEvent.FromModel,
            ToModel = streamEvent.ToModel,
            Content = streamEvent.Content,
            UsedFallback = streamEvent.UsedFallback,
            ErrorCode = streamEvent.ErrorCode,
            ErrorMessage = streamEvent.ErrorMessage,
            ToolCallDelta = streamEvent.ToolCallDelta is null ? null : new LLMToolCallDelta
            {
                Index = streamEvent.ToolCallDelta.Index,
                Id = streamEvent.ToolCallDelta.Id,
                Type = streamEvent.ToolCallDelta.Type,
                Name = streamEvent.ToolCallDelta.Name,
                Arguments = streamEvent.ToolCallDelta.Arguments
            },
            ToolCalls = streamEvent.ToolCalls.Select(toolCall => new LLMToolCall
            {
                Id = toolCall.Id,
                Type = toolCall.Type,
                Function = new LLMToolFunctionCall
                {
                    Name = toolCall.Function.Name,
                    Arguments = toolCall.Function.Arguments
                }
            }).ToList(),
            FinishReason = streamEvent.FinishReason,
            ToolResults = streamEvent.ToolResults.Select(result => new LLMToolExecutionResult
            {
                ToolCallId = result.ToolCallId,
                ToolName = result.ToolName,
                Success = result.Success,
                Content = result.Content,
                ErrorCode = result.ErrorCode
            }).ToList()
        };
    }
}
