using Microsoft.EntityFrameworkCore;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Infrastructure.AI.Memory;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Context;

public sealed class CreationAgentContext(
    IMemoryProvider memory,
    IUserContext user,
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator) : ICreationAgentContext
{
    private const int FilteredHistoryMessages = 8;
    private const int FullHistoryMessages = 20;
    private const int ReservedOutputTokens = 8_000;
    private const int DefaultContextWindowTokens = 32_000;

    public async Task<AgentContext> BuildContextAsync(
        string workId,
        string sessionId,
        string agentName,
        string primaryModel,
        bool includeMemory,
        bool filterHistory,
        int contextWindowTokens,
        CancellationToken cancellationToken = default)
    {
        var ctx = new AgentContext
        {
            UserId = user.UserId,
            RequestId = Guid.NewGuid().ToString()
        };

        if (string.IsNullOrWhiteSpace(workId) || string.IsNullOrWhiteSpace(sessionId))
            return ctx;

        var ownsSession = await dbContext.AICreationSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId &&
                           s.WorkId == workId &&
                           s.UserId == user.UserId,
                cancellationToken);

        if (!ownsSession)
            return ctx;

        var memorySnapshot = includeMemory
            ? await memory.LoadSessionMemoryAsync(user.UserId, workId, sessionId, cancellationToken)
            : SessionMemorySnapshot.Empty;

        var recentMessages = await LoadRecentMessagesAsync(sessionId, filterHistory, cancellationToken);
        var messages = new List<ChatMessage>();

        if (includeMemory && !string.IsNullOrWhiteSpace(memorySnapshot.Summary))
        {
            ctx.ProjectMemory = memorySnapshot.Summary;
            ctx.SnapshotId = memorySnapshot.SnapshotId;
            messages.Add(ChatMessage.System($"[Session Memory]\n{memorySnapshot.Summary}"));
        }

        messages.AddRange(recentMessages);

        var memoryTokens = EstimateTokens(ctx.ProjectMemory);
        var recentTokens = EstimateTokens(recentMessages);
        var totalTokens = EstimateTokens(messages);
        var budget = ResolveInputBudget(contextWindowTokens);
        var wasTrimmed = false;

        while (messages.Count > 1 && totalTokens > budget)
        {
            var removeIndex = messages[0] is SystemMessage ? 1 : 0;
            messages.RemoveAt(removeIndex);
            totalTokens = EstimateTokens(messages);
            wasTrimmed = true;
        }

        ctx.ConversationHistory = messages;
        ctx.HistoryMessage = recentMessages.Select(FormatMessage).Where(x => x.Length > 0).ToList();
        ctx.MemoryTokenCount = memoryTokens;
        ctx.RecentContextTokenCount = recentTokens;
        ctx.InputTokenCount = totalTokens;
        ctx.WasTrimmed = wasTrimmed;

        await WriteAssemblyLogAsync(
            workId,
            sessionId,
            agentName,
            primaryModel,
            ctx,
            cancellationToken);

        return ctx;
    }

    private async Task<List<ChatMessage>> LoadRecentMessagesAsync(
        string sessionId,
        bool filterHistory,
        CancellationToken cancellationToken)
    {
        var take = filterHistory ? FilteredHistoryMessages : FullHistoryMessages;

        var rows = await dbContext.AICreationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.Role != "tool")
            .OrderByDescending(m => m.TurnNumber)
            .ThenByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(m => m.TurnNumber)
            .ThenBy(m => m.CreatedAt)
            .Select(ToChatMessage)
            .Where(m => m != null)
            .ToList();
    }

    private async Task WriteAssemblyLogAsync(
        string workId,
        string sessionId,
        string agentName,
        string primaryModel,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        dbContext.ContextAssemblyLogs.Add(new ContextAssemblyLogEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = user.UserId,
            WorkId = workId,
            SessionId = sessionId,
            ContextMode = agentName ?? string.Empty,
            SnapshotId = context.SnapshotId,
            PrimaryModelId = primaryModel ?? string.Empty,
            InputTokenCount = context.InputTokenCount,
            CoreSettingTokens = context.MemoryTokenCount,
            RecentContextTokens = context.RecentContextTokenCount,
            RetrievedContextTokens = 0,
            SelectedChunkIdsJson = JsonHelper.Serialize(string.IsNullOrWhiteSpace(context.SnapshotId)
                ? Array.Empty<string>()
                : new[] { context.SnapshotId }),
            AssemblySummary = $"messages={context.ConversationHistory.Count}; trimmed={context.WasTrimmed}",
            UsedFallback = false,
            CreateBy = user.UserId,
            UpdateBy = user.UserId,
            CreateAt = now,
            UpdateAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ChatMessage ToChatMessage(Domain.Entities.AI.AICreationMessageEntity message)
    {
        if (message.Role == "user")
            return ChatMessage.User(message.Content);

        if (message.Role == "assistant")
            return ChatMessage.Assistant(message.Content);

        return null;
    }

    private static int ResolveInputBudget(int contextWindowTokens)
    {
        var window = contextWindowTokens > 0 ? contextWindowTokens : DefaultContextWindowTokens;
        return Math.Max(4_000, window - ReservedOutputTokens);
    }

    private static int EstimateTokens(IEnumerable<ChatMessage> messages)
    {
        return messages.Sum(message => EstimateTokens(ExtractText(message)));
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var chineseCount = 0;
        foreach (var c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF)
                chineseCount++;
        }

        var otherCount = text.Length - chineseCount;
        return (int)(chineseCount * 1.5) + (int)(otherCount * 0.25);
    }

    private static string FormatMessage(ChatMessage message)
    {
        return message switch
        {
            UserMessage userMessage => $"user: {ExtractUserText(userMessage)}",
            AssistantMessage assistantMessage => $"assistant: {assistantMessage.Content}",
            _ => string.Empty
        };
    }

    private static string ExtractText(ChatMessage message)
    {
        return message switch
        {
            SystemMessage systemMessage => systemMessage.Content,
            UserMessage userMessage => ExtractUserText(userMessage),
            AssistantMessage assistantMessage => assistantMessage.Content ?? string.Empty,
            ToolMessage toolMessage => toolMessage.Content ?? string.Empty,
            _ => string.Empty
        };
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
}
