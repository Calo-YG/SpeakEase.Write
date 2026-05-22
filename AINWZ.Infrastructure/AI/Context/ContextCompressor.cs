using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Context;

// 上下文压缩器：当对话历史超出 token 预算时，调用 LLM 将早期消息压缩为结构摘要，保留核心决策和风格特征
public sealed class ContextCompressor(IChatCompatible llm, ILogger<ContextCompressor> logger) : IContextCompressor
{
    private readonly IChatCompatible _llm = llm;
    private readonly ILogger<ContextCompressor> _logger = logger;

    private const int RecentRounds = 4;          // 保留最近 N 轮完整对话不被压缩
    private const int ContextWindowTokens = 128_000; // 上下文窗口上限
    private const int ReservedTokens = 18_000;   // 预留 token 配额（系统提示 + 输出）
    private const int SummaryMaxTokens = 800;     // 摘要生成的最大 token 数

    // 摘要生成提示词：要求保留核心需求、关键决策、未解决问题、写作风格特征
    private const string SummaryPrompt =
        """
        请将以下对话历史压缩为简明摘要，保留：
        1. 用户的核心需求和偏好
        2. 已完成的关键决策
        3. 尚未解决的问题
        4. 对后续写作有参考价值的信息
        5. 重要：保留前文写作中使用的叙事视角、时态、语言风格特征（如"第三人称有限视角""过去时""冷峻克制""口语化对话多"等标签）
        限制在 500 字以内，使用简洁的要点式表述。
        在摘要末尾用一行单独标注：[风格标签] 视角/时态/语言特征
        """;

    // 主压缩方法：判断是否需要压缩，需要则将早期消息替换为摘要
    public async Task<List<ChatMessage>> CompressAsync(
        List<ChatMessage> history,
        string model,
        CancellationToken ct)
    {
        // 历史轮数不足时不压缩
        if (history is not { Count: > RecentRounds * 2 })
            return history ?? new List<ChatMessage>();

        // 计算当前 token 数，判断是否超过预算
        var totalTokens = EstimateTokens(history);
        var budget = ContextWindowTokens - ReservedTokens;

        if (totalTokens <= budget)
            return history;

        _logger.LogInformation(
            "History tokens {Total} exceeds budget {Budget}, compressing {Count} messages",
            totalTokens, budget, history.Count);

        // 保留最近 N 轮完整消息，将其余的早期消息压缩为摘要
        var recentCount = Math.Min(RecentRounds * 2, history.Count);
        var recentMessages = history.GetRange(history.Count - recentCount, recentCount);
        var olderMessages = history.GetRange(0, history.Count - recentCount);

        var summary = await GenerateSummaryAsync(olderMessages, model, ct);

        // 将摘要作为系统消息，后续接最近消息
        var result = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(summary))
            result.Add(ChatMessage.System($"[对话摘要]\n{summary}"));
        result.AddRange(recentMessages);

        _logger.LogInformation(
            "Compressed {OldCount} older messages into summary, keeping {RecentCount} recent messages",
            olderMessages.Count, recentMessages.Count);

        return result;
    }

    // 调用 LLM 将早期对话消息压缩为结构摘要
    private async Task<string> GenerateSummaryAsync(
        List<ChatMessage> olderMessages,
        string model,
        CancellationToken ct)
    {
        try
        {
            var conversationText = string.Join("\n", olderMessages.Select(FormatMessage));
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(SummaryPrompt),
                ChatMessage.User(conversationText)
            };

            var ctx = new LLMTurnContext
            {
                Model = model,
                Temperature = 0.3,
                MaxTokens = SummaryMaxTokens
            };

            var result = await _llm.ChatAsync(ctx, messages, [], ct);
            return result?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate conversation summary, skipping compression");
            return string.Empty;
        }
    }

    // 将 ChatMessage 格式化为纯文本（用于输入 LLM 做摘要）
    private static string FormatMessage(ChatMessage msg)
    {
        return msg switch
        {
            SystemMessage s => $"[系统] {s.Content}",
            UserMessage u => $"[用户] {ExtractText(u.Content)}",
            AssistantMessage a => $"[助手] {a.Content}",
            ToolMessage t => $"[工具] {t.Content}",
            _ => string.Empty
        };
    }

    // 从对象内容中提取纯文本（支持 string 和 ContentPart[]）
    private static string ExtractText(object content)
    {
        return content switch
        {
            string s => s,
            List<ContentPart> parts => string.Join(" ",
                parts.Where(p => p.Type == "text" && !string.IsNullOrEmpty(p.Text))
                     .Select(p => p.Text)),
            _ => content?.ToString() ?? string.Empty
        };
    }

    // 估算消息列表的总 token 数
    private static int EstimateTokens(List<ChatMessage> messages)
    {
        var total = 0;
        foreach (var msg in messages)
        {
            var text = msg switch
            {
                SystemMessage s => s.Content,
                UserMessage u => ExtractText(u.Content),
                AssistantMessage a => a.Content ?? string.Empty,
                ToolMessage t => t.Content ?? string.Empty,
                _ => string.Empty
            };
            total += EstimateTokens(text);
        }
        return total;
    }

    // 估算单段文本的 token 数：中文约 1.5 tokens/字，英文约 0.25 tokens/字
    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var chineseCount = 0;
        foreach (var c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF) chineseCount++;
        }
        var otherCount = text.Length - chineseCount;
        return (int)(chineseCount * 1.5) + (int)(otherCount * 0.25);
    }
}
