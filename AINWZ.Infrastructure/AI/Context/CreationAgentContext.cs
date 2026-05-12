using System.Text;
using System.Text.RegularExpressions;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.AI.Memory;

namespace SpeakEase.Write.Infrastructure.AI.Context;

public sealed class CreationAgentContext : ICreationAgentContext
{
    private readonly IMemoryProvider _memory;
    private readonly IUserContext _user;

    public CreationAgentContext(
        IMemoryProvider memory,
        IUserContext user)
    {
        _memory = memory;
        _user = user;
    }

    public async Task<AgentContext> BuildContext(string workId, CancellationToken cancellationToken = default)
    {
        var ctx = new AgentContext
        {
            HistoryMessage = new List<string>(),
            RequestId = Guid.NewGuid().ToString()
        };

        if (string.IsNullOrEmpty(workId))
        {
            ctx.ProjectMemory = string.Empty;
            return ctx;
        }

        var mem = await _memory.LoadAsync(_user.UserId, workId, cancellationToken);
        ctx.ProjectMemory = FormatProjectMemory(mem);

        return ctx;
    }

    private static string FormatProjectMemory(MemoryContext mem)
    {
        if (string.IsNullOrEmpty(mem.WorkTitle) && mem.Characters.Count == 0 && mem.RecentChapters.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("# 作品上下文");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(mem.WorkTitle))
        {
            sb.AppendLine($"**作品**：{mem.WorkTitle}");
            sb.AppendLine($"**类型**：{mem.Genre} | **视角**：{mem.Perspective} | **总字数**：{mem.TotalWordCount}");
            if (!string.IsNullOrEmpty(mem.WorkSummary))
                sb.AppendLine($"**简介**：{mem.WorkSummary}");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(mem.WorldSettingSummary))
        {
            sb.AppendLine("## 世界观概要");
            sb.AppendLine(mem.WorldSettingSummary);
            sb.AppendLine();
        }

        if (mem.Characters.Count > 0)
        {
            sb.AppendLine("## 人物");
            foreach (var c in mem.Characters)
            {
                var roleInfo = string.Join(" | ", new[] { c.Identity, c.Personality }.Where(x => !string.IsNullOrEmpty(x)));
                var nameLine = string.IsNullOrEmpty(roleInfo) ? c.Name : $"{c.Name}（{roleInfo}）";
                sb.AppendLine($"- {nameLine}");
            }
            sb.AppendLine();
        }

        if (mem.OutlineNodes.Count > 0)
        {
            sb.AppendLine("## 大纲");
            foreach (var n in mem.OutlineNodes)
            {
                var descSuffix = string.IsNullOrEmpty(n.Description) ? "" : $" — {n.Description}";
                var chapterTag = string.IsNullOrEmpty(n.ChapterId) ? "" : " [已有章节]";
                sb.AppendLine($"- {n.Title}{descSuffix}{chapterTag}");
            }
            sb.AppendLine();
        }

        if (mem.RecentChapters.Count > 0)
        {
            sb.AppendLine("## 最近章节");
            foreach (var c in mem.RecentChapters)
                sb.AppendLine($"- 第{c.Sequence}章 {c.Title}（{c.WordCount}字·{c.Status}）{(string.IsNullOrEmpty(c.Summary) ? "" : $"：{c.Summary}")}");
            sb.AppendLine();
        }

        if (mem.ActiveForeshadowings.Count > 0)
        {
            sb.AppendLine("## 待回收伏笔");
            foreach (var f in mem.ActiveForeshadowings)
                sb.AppendLine($"- {f.Title} [{f.Status}]");
            sb.AppendLine();
        }

        if (mem.TimelineEvents.Count > 0)
        {
            sb.AppendLine("## 故事时间线");
            foreach (var t in mem.TimelineEvents)
            {
                sb.AppendLine($"- {t.EventTime:yyyy-MM-dd} [{t.EventType}] {t.Title}");
                if (!string.IsNullOrEmpty(t.Description))
                    sb.AppendLine($"  > {t.Description}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(mem.StyleReference))
        {
            sb.AppendLine("## 文风参考（以下摘自最新已完成章节正文，写作时严格模仿其句式、节奏、用词和叙述风格）");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(mem.StyleReference);
            sb.AppendLine("```");
            sb.AppendLine();

            var fingerprint = ExtractStyleFingerprint(mem.StyleReference);
            if (!string.IsNullOrEmpty(fingerprint))
            {
                sb.AppendLine("## 风格指纹（前文章节的量化风格特征，写作时务必匹配）");
                sb.AppendLine(fingerprint);
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string ExtractStyleFingerprint(string styleText)
    {
        if (string.IsNullOrEmpty(styleText) || styleText.Length < 200)
            return string.Empty;

        var sentences = Regex.Split(styleText, @"[。！？；\n]+")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (sentences.Count < 5)
            return string.Empty;

        var shortCount = sentences.Count(s => s.Length <= 15);
        var mediumCount = sentences.Count(s => s.Length > 15 && s.Length <= 35);
        var longCount = sentences.Count(s => s.Length > 35);

        var totalSentences = sentences.Count;
        var shortPct = (int)(shortCount * 100.0 / totalSentences);
        var mediumPct = (int)(mediumCount * 100.0 / totalSentences);
        var longPct = (int)(longCount * 100.0 / totalSentences);

        var quoteMatches = Regex.Matches(styleText, @"[""「『""'']");
        var dialogueLineCount = Regex.Matches(styleText, @"[""「『][^""」』]*[""」』]").Count;
        var dialogueDensity = (int)(dialogueLineCount * 100.0 / Math.Max(1, sentences.Count));

        var paragraphs = styleText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
        var minParaLen = paragraphs.Count > 0 ? paragraphs.Min(p => p.Length) : 0;
        var maxParaLen = paragraphs.Count > 0 ? paragraphs.Max(p => p.Length) : 0;

        var sentenceStructure = "";
        if (shortPct >= 40)
            sentenceStructure = "短句主导（短句≥40%），节奏快，推进力强";
        else if (longPct >= 30)
            sentenceStructure = "长句铺陈（长句≥30%），描写细致，节奏舒缓";
        else
            sentenceStructure = "长短均衡（短句和长句各占一定比例），节奏平稳";

        var dialogueStyle = dialogueDensity >= 15 ? "对话密集" : "叙述为主";

        return $"""
- **句式分布**：短句（≤15字）{shortPct}% | 中句（15-35字）{mediumPct}% | 长句（>35字）{longPct}%（共{totalSentences}句）
- **句式特征**：{sentenceStructure}
- **对话密度**：对话行占比约 {dialogueDensity}%（{dialogueStyle}）
- **段落呼吸**：最短段 {minParaLen} 字 | 最长段 {maxParaLen} 字（共{paragraphs.Count}段）
""";
    }
}
