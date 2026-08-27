using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.Ids;

namespace SpeakEase.Write.Infrastructure.AI;

public sealed class LoggingChatCompatible(
    OpenAICompatible inner,
    IWriteDbContext db,
    IUserContext user,
    ISnowflakeIdGenerator idGenerator,
    ILogger<LoggingChatCompatible> logger) : IChatCompatible
{
    public async Task<LLMTurnResult> ChatAsync(
        LLMTurnContext context,
        List<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await inner.ChatAsync(context, messages, tools, cancellationToken);
            await WriteLogAsync("chat", context, messages, tools, result, null, cancellationToken);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await WriteLogAsync("chat", context, messages, tools, null, ex.Message, cancellationToken);
            throw;
        }
    }

    public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
        LLMTurnContext context,
        List<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LLMTurnResult finalResult = null;
        var completed = false;
        var enumerator = inner.StreamAsync(context, messages, tools, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                LLMTurnChunk chunk = null;
                Exception moveNextException = null;
                var hasNext = false;

                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (hasNext)
                        chunk = enumerator.Current;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    moveNextException = ex;
                }

                if (moveNextException is not null)
                {
                    await WriteLogAsync("stream", context, messages, tools, finalResult, moveNextException.Message, cancellationToken);
                    ExceptionDispatchInfo.Capture(moveNextException).Throw();
                }

                if (!hasNext)
                    break;

                if (chunk?.Type == "done")
                    finalResult = chunk.TurnResult;

                yield return chunk;
            }

            completed = true;
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        if (completed)
            await WriteLogAsync("stream", context, messages, tools, finalResult, null, cancellationToken);
    }

    private async Task WriteLogAsync(
        string callType,
        LLMTurnContext context,
        List<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LLMTurnResult result,
        string exceptionMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.Now;
            var success = result?.Success == true && string.IsNullOrWhiteSpace(exceptionMessage);

            db.LlmCallLogs.Add(new LLMCallLogEntity
            {
                Id = idGenerator.NextIdString(),
                CallType = callType,
                SkillName = string.Empty,
                RequestSummary = Truncate(FindFirstUserMessage(messages), 500),
                ResponseSummary = Truncate(result?.Content, 500),
                PrimaryModel = context.Model,
                FinalModel = result?.Model ?? context.Model,
                UsedFallback = false,
                FallbackModel = string.Empty,
                RequestId = result?.RequestId,
                FinishReason = result?.FinishReason,
                ToolCallsSummary = tools is { Count: > 0 } ? $"available={tools.Count}" : string.Empty,
                ToolResultsSummary = result?.HasToolCalls == true ? $"requested={result.ToolCalls.Count}" : string.Empty,
                Success = success,
                ErrorMessage = Truncate(exceptionMessage ?? result?.ErrorMessage, 500),
                StopReason = result?.FinishReason,
                Iterations = 1,
                OwnerId = user.UserId,
                CreateBy = user.UserId,
                UpdateBy = user.UserId,
                CreateAt = now,
                UpdateAt = now
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to write LLM call log.");
        }
    }

    private static string FindFirstUserMessage(IEnumerable<ChatMessage> messages)
    {
        if (messages == null)
            return string.Empty;

        foreach (var message in messages)
        {
            if (message is UserMessage userMessage)
                return ExtractUserText(userMessage);
        }

        return string.Empty;
    }

    private static string ExtractUserText(UserMessage message)
    {
        return message.Content switch
        {
            string value => value,
            List<ContentPart> parts => string.Join(" ",
                parts.Where(p => p.Type == "text" && !string.IsNullOrEmpty(p.Text))
                    .Select(p => p.Text)),
            _ => message.Content?.ToString() ?? string.Empty
        };
    }

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= maxChars ? compact : compact[..maxChars] + "...";
    }
}
