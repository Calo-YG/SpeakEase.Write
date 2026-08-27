using Microsoft.EntityFrameworkCore;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Domain.Entities.Memory;
using ApplicationMemoryProvider = SpeakEase.Write.Application.Abstractions.AI.IMemoryProvider;
using SessionMemorySnapshot = SpeakEase.Write.Application.Abstractions.AI.SessionMemorySnapshot;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Context;

// Agent 上下文构建器：拼接项目记忆 + 历史对话 + token 预算裁剪，生成 LLM 可用的完整上下文
public sealed class CreationAgentContext(
    ApplicationMemoryProvider memory,
    IUserContext user,
    IWriteDbContext dbContext,
    ISnowflakeIdGenerator idGenerator) : ICreationAgentContext
{
    private const int FilteredHistoryTurns = 8;     // 筛选模式：仅保留最近 8 个完整轮次
    private const int FullHistoryTurns = 20;         // 全历史模式：保留最近 20 个完整轮次
    private const int ReservedOutputTokens = 8_000;     // 为 LLM 输出预留的 token 配额
    private const int DefaultContextWindowTokens = 32_000; // 默认上下文窗口大小

    // 核心构建方法：组装会话上下文（记忆、历史、token 预算裁剪）
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

        // 验证当前用户是否拥有该会话
        var ownsSession = await dbContext.AICreationSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId &&
                           s.WorkId == workId &&
                           s.UserId == user.UserId,
                cancellationToken);

        if (!ownsSession)
            return ctx;

        // 根据 Agent 元数据决定是否加载会话记忆
        var memorySnapshot = includeMemory
            ? await memory.LoadSessionMemoryAsync(user.UserId, workId, sessionId, cancellationToken)
            : SessionMemorySnapshot.Empty;
        var projectFacts = includeMemory
            ? await memory.LoadProjectFactsAsync(user.UserId, workId, cancellationToken)
            : Array.Empty<SpeakEase.Write.Application.Abstractions.AI.MemoryFact>();

        var recentMessages = await LoadRecentMessagesAsync(
            sessionId,
            filterHistory,
            memorySnapshot.CoveredToTurn,
            cancellationToken);
        var messages = new List<ChatMessage>();

        // 注入项目记忆为系统消息（第一个消息）
        if (includeMemory && !string.IsNullOrWhiteSpace(memorySnapshot.Summary))
        {
            ctx.ProjectMemory = memorySnapshot.Summary;
            ctx.SnapshotId = memorySnapshot.SnapshotId;
            messages.Add(ChatMessage.System($"[Session Memory]\n{memorySnapshot.Summary}"));
        }

        if (projectFacts.Count > 0)
        {
            var factText = string.Join(
                "\n",
                projectFacts.Take(64).Select(x => $"- [{x.Category}] {x.Key}: {x.Value}"));
            messages.Insert(0, ChatMessage.System($"[Project Facts]\n{factText}"));
        }

        messages.AddRange(recentMessages);

        // 估算各部分的 token 数，计算输入预算
        var memoryTokens = EstimateTokens(ctx.ProjectMemory);
        var recentTokens = EstimateTokens(recentMessages);
        var totalTokens = EstimateTokens(messages);
        var budget = ResolveInputBudget(contextWindowTokens);
        var wasTrimmed = false;

        // 超出预算时逐条删除最早的消息（保留第一条系统消息），直到满足预算
        while (messages.Count > 1 && totalTokens > budget)
        {
            var removeIndex = messages[0] is SystemMessage ? 1 : 0;
            messages.RemoveAt(removeIndex);
            totalTokens = EstimateTokens(messages);
            wasTrimmed = true;
        }

        // 只有一条超长系统/记忆消息时也必须裁剪，不能因为消息数为 1 而绕过预算。
        if (totalTokens > budget && messages.Count == 1 && messages[0] is SystemMessage systemMessage)
        {
            systemMessage.Content = TruncateToBudget(systemMessage.Content, budget);
            totalTokens = EstimateTokens(messages);
            wasTrimmed = true;
        }

        ctx.ConversationHistory = messages;
        ctx.HistoryMessage = recentMessages.Select(FormatMessage).Where(x => x.Length > 0).ToList();
        ctx.MemoryTokenCount = memoryTokens;
        ctx.RecentContextTokenCount = recentTokens;
        ctx.InputTokenCount = totalTokens;
        ctx.WasTrimmed = wasTrimmed;

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
    private async Task<List<ChatMessage>> LoadRecentMessagesAsync(
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
            return new List<ChatMessage>();

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
            .Select(ToChatMessage)
            .Where(m => m != null)
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

    // 计算可用的输入 token 预算：context window - 预留输出 tokens
    private static int ResolveInputBudget(int contextWindowTokens)
    {
        var window = contextWindowTokens > 0 ? contextWindowTokens : DefaultContextWindowTokens;
        var reservedOutput = Math.Min(ReservedOutputTokens, Math.Max(1_000, window / 4));
        return Math.Max(1, window - reservedOutput);
    }

    // 估算消息列表的总 token 数（基于字符数近似：中文×1.5，英文×0.25）
    private static int EstimateTokens(IEnumerable<ChatMessage> messages)
    {
        return messages.Sum(message => EstimateTokens(ExtractText(message)));
    }

    // 估算单段文本的 token 数：中文每字约 1.5 tokens，其他字符每字约 0.25 tokens
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

    // 从 ChatMessage 中提取纯文本内容（处理多种内容格式）
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

    private static string TruncateText(string value, int maxCharacters)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
            return value ?? string.Empty;

        return value[..maxCharacters] + "\n[context truncated]";
    }

    private static string TruncateToBudget(string value, int budget)
    {
        if (string.IsNullOrEmpty(value) || budget <= 0)
            return string.Empty;

        var low = 0;
        var high = value.Length;
        var best = string.Empty;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var candidate = TruncateText(value, middle);
            if (EstimateTokens(candidate) <= budget)
            {
                best = candidate;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return best;
    }
}
