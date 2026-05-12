using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Context;

public sealed class ContextCompressor(IChatCompatible llm, ILogger<ContextCompressor> logger) : IContextCompressor
{
    private readonly IChatCompatible _llm = llm;
    private readonly ILogger<ContextCompressor> _logger = logger;

    private const int RecentRounds = 4;
    private const int ContextWindowTokens = 128_000;
    private const int ReservedTokens = 18_000;
    private const int SummaryMaxTokens = 800;

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

    public async Task<List<ChatMessage>> CompressAsync(
        List<ChatMessage> history,
        string model,
        CancellationToken ct)
    {
        if (history is not { Count: > RecentRounds * 2 })
            return history ?? new List<ChatMessage>();

        var totalTokens = EstimateTokens(history);
        var budget = ContextWindowTokens - ReservedTokens;

        if (totalTokens <= budget)
            return history;

        _logger.LogInformation(
            "History tokens {Total} exceeds budget {Budget}, compressing {Count} messages",
            totalTokens, budget, history.Count);

        var recentCount = Math.Min(RecentRounds * 2, history.Count);
        var recentMessages = history.GetRange(history.Count - recentCount, recentCount);
        var olderMessages = history.GetRange(0, history.Count - recentCount);

        var summary = await GenerateSummaryAsync(olderMessages, model, ct);

        var result = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(summary))
            result.Add(ChatMessage.System($"[对话摘要]\n{summary}"));
        result.AddRange(recentMessages);

        _logger.LogInformation(
            "Compressed {OldCount} older messages into summary, keeping {RecentCount} recent messages",
            olderMessages.Count, recentMessages.Count);

        return result;
    }

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
