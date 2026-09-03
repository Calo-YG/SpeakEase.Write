using Microsoft.EntityFrameworkCore;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Domain.Entities.Memory;
using ApplicationMemoryProvider = SpeakEase.Write.Application.Abstractions.AI.IMemoryProvider;
using SessionMemorySnapshot = SpeakEase.Write.Application.Abstractions.AI.SessionMemorySnapshot;
using SpeakEase.Write.Application.Abstractions.Memory;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Context;

// Agent 上下文构建器：拼接项目记忆 + 历史对话 + token 预算裁剪，生成 LLM 可用的完整上下文
public sealed class CreationAgentContext(
    ApplicationMemoryProvider memory,
    IUserContext user,
    IMemoryDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    LayeredContextAssembler layeredContextAssembler = null,
    IMemoryContextProvider memoryContextProvider = null) : ICreationAgentContext
{
    private const int FilteredHistoryTurns = 8;     // 筛选模式：仅保留最近 8 个完整轮次
    private const int FullHistoryTurns = 20;         // 全历史模式：保留最近 20 个完整轮次
    public Task<AgentContext> BuildContextAsync(
        string workId,
        string sessionId,
        string agentName,
        string primaryModel,
        bool includeMemory,
        bool filterHistory,
        int contextWindowTokens,
        CancellationToken cancellationToken = default)
        => BuildContextAsync(
            workId,
            sessionId,
            agentName,
            primaryModel,
            includeMemory,
            filterHistory,
            contextWindowTokens,
            string.Empty,
            cancellationToken);

    // 核心构建方法：组装 L1-L4，并为 L0 当前输入和模型输出预留预算。
    public async Task<AgentContext> BuildContextAsync(
        string workId,
        string sessionId,
        string agentName,
        string primaryModel,
        bool includeMemory,
        bool filterHistory,
        int contextWindowTokens,
        string currentUserMessage,
        CancellationToken cancellationToken = default)
    {
        var ctx = new AgentContext
        {
            UserId = user.UserId,
            RequestId = Guid.NewGuid().ToString()
        };

        if (string.IsNullOrWhiteSpace(workId) || string.IsNullOrWhiteSpace(sessionId))
            return ctx;

        // 验证当前用户是否拥有该会话
        var ownsSession = await dbContext.AICreationSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId &&
                           s.WorkId == workId &&
                           s.UserId == user.UserId,
                cancellationToken);

        if (!ownsSession)
            return ctx;

        var layers = includeMemory && memoryContextProvider is not null
            ? await memoryContextProvider.LoadAsync(new MemoryContextRequest
            {
                UserId = user.UserId,
                WorkId = workId,
                SessionId = sessionId,
                Query = currentUserMessage
            }, cancellationToken)
            : null;
        var memorySnapshot = includeMemory
            ? layers?.Session ?? await memory.LoadSessionMemoryAsync(user.UserId, workId, sessionId, cancellationToken)
            : SessionMemorySnapshot.Empty;
        var projectFacts = includeMemory
            ? layers?.ProjectFacts ?? await memory.LoadProjectFactsAsync(user.UserId, workId, cancellationToken)
            : Array.Empty<SpeakEase.Write.Application.Abstractions.AI.MemoryFact>();

        var recentTurns = await LoadRecentTurnsAsync(
            sessionId,
            filterHistory,
            memorySnapshot.CoveredToTurn,
            cancellationToken);
        var recentMessages = recentTurns.SelectMany(x => x.Messages).ToList();
        var factText = string.Join(
            "\n",
            projectFacts.Take(64).Select(x => $"- [{x.Category}] {x.Key}: {x.Value}"));
        var retrievedText = layers is null
            ? string.Empty
            : string.Join("\n", layers.RetrievedArtifacts.Select(x =>
                $"- artifact:{x.ArtifactId} [{x.ContentType}] {x.Summary}"));
        var assembler = layeredContextAssembler ?? new LayeredContextAssembler();
        var assembled = assembler.Assemble(new LayeredContextAssemblyRequest
        {
            CurrentUserMessage = currentUserMessage,
            ProjectFacts = factText,
            SessionMemory = memorySnapshot.Summary,
            RetrievedContext = retrievedText,
            ConversationTurns = recentTurns,
            ContextWindowTokens = contextWindowTokens
        });

        ctx.ProjectMemory = memorySnapshot.Summary;
        ctx.SnapshotId = memorySnapshot.SnapshotId;
        ctx.ConversationHistory = assembled.Messages;
        ctx.HistoryMessage = recentMessages.Select(FormatMessage).Where(x => x.Length > 0).ToList();
        ctx.MemoryTokenCount = LayeredContextAssembler.EstimateTokens(memorySnapshot.Summary);
        ctx.RecentContextTokenCount = LayeredContextAssembler.EstimateTokens(recentMessages);
        ctx.InputTokenCount = assembled.InputTokenCount;
        ctx.WasTrimmed = assembled.WasTrimmed;

        // 记录上下文组装日志（用于调试和分析）
        await WriteAssemblyLogAsync(
            workId,
            sessionId,
            agentName,
            primaryModel,
            ctx,
            cancellationToken);

        return ctx;
    }

    // 加载最近的会话消息（排除工具调用消息），支持筛选/全量模式
    private async Task<List<LayeredConversationTurn>> LoadRecentTurnsAsync(
        string sessionId,
        bool filterHistory,
        int coveredToTurn,
        CancellationToken cancellationToken)
    {
        var take = filterHistory ? FilteredHistoryTurns : FullHistoryTurns;

        var turnNumbers = await dbContext.AICreationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.Role != "tool" && m.TurnNumber > coveredToTurn)
            .Select(m => m.TurnNumber)
            .Distinct()
            .OrderByDescending(turn => turn)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (turnNumbers.Count == 0)
            return new List<LayeredConversationTurn>();

        // 先选轮次，再加载这些轮次的完整消息，避免 Take(messageCount) 拆开一轮对话。
        var rows = await dbContext.AICreationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId &&
                        m.TurnNumber > coveredToTurn &&
                        m.Role != "tool" &&
                        turnNumbers.Contains(m.TurnNumber))
            .OrderBy(m => m.TurnNumber)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.TurnNumber)
            .OrderBy(x => x.Key)
            .Select(group => new LayeredConversationTurn
            {
                TurnNumber = group.Key,
                Messages = group
                    .Select(ToChatMessage)
                    .Where(message => message is not null)
                    .ToList()
            })
            .Where(turn => turn.Messages.Count > 0)
            .ToList();
    }

    // 写入上下文组装日志，记录每次 Agent 调用时的上下文构成信息
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

    // 将数据库实体转为 ChatMessage 对象（仅处理 user 和 assistant 角色）
    private static ChatMessage ToChatMessage(Domain.Entities.AI.AICreationMessageEntity message)
    {
        if (message.Role == "user")
            return ChatMessage.User(message.Content);

        if (message.Role == "assistant")
            return ChatMessage.Assistant(message.Content);

        return null;
    }

    // 格式化消息为人类可读文本（日志输出用）
    private static string FormatMessage(ChatMessage message)
    {
        return message switch
        {
            UserMessage userMessage => $"user: {ExtractUserText(userMessage)}",
            AssistantMessage assistantMessage => $"assistant: {assistantMessage.Content}",
            _ => string.Empty
        };
    }

    // 提取 UserMessage 中的文本（支持 string 和 ContentPart[] 两种格式）
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
