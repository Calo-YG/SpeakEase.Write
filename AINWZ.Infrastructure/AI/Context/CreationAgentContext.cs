using System.Text;
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

        return sb.ToString().TrimEnd();
    }
}
