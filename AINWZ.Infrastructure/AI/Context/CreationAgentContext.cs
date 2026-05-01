using System.Text;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.AI.Memory;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Context;

public sealed class CreationAgentContext : ICreationAgentContext
{
    private readonly IMemoryProvider _memory;
    private readonly IUserContext _user;
    private readonly BlackboardHolder _blackboardHolder;

    public CreationAgentContext(
        IMemoryProvider memory,
        IUserContext user,
        BlackboardHolder blackboardHolder)
    {
        _memory = memory;
        _user = user;
        _blackboardHolder = blackboardHolder;
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

        var blackboard = _blackboardHolder.Blackboard;
        if (blackboard != null && blackboard.WorkId == workId)
        {
            ctx.ProjectMemory = FormatFromBlackboard(blackboard);
        }
        else
        {
            var mem = await _memory.LoadAsync(_user.UserId, workId, cancellationToken);
            ctx.ProjectMemory = FormatProjectMemory(mem);
        }

        return ctx;
    }

    private static string FormatFromBlackboard(WritingBlackboard bb)
    {
        if (string.IsNullOrEmpty(bb.WorkTitle) && bb.Characters.Count == 0 && bb.RecentChapters.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("# 作品上下文");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(bb.WorkTitle))
        {
            sb.AppendLine($"**作品**：{bb.WorkTitle}");
            sb.AppendLine($"**类型**：{bb.Meta.Genre} | **视角**：{bb.Meta.Perspective} | **总字数**：{bb.Meta.TotalWordCount}");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(bb.WorldSetting.WorldRules))
        {
            sb.AppendLine("## 世界观概要");
            sb.AppendLine(bb.WorldSetting.WorldRules);
            sb.AppendLine();
        }

        if (bb.Characters.Count > 0)
        {
            sb.AppendLine("## 人物");
            foreach (var c in bb.Characters)
            {
                var roleInfo = string.Join(" | ", new[] { c.Personality, c.Traits }.Where(x => !string.IsNullOrEmpty(x)));
                var nameLine = string.IsNullOrEmpty(roleInfo) ? c.Name : $"{c.Name}（{roleInfo}）";
                sb.AppendLine($"- {nameLine}");
            }
            sb.AppendLine();
        }

        if (bb.Outline.OutlineNodes.Count > 0)
        {
            sb.AppendLine("## 大纲");
            foreach (var n in bb.Outline.OutlineNodes)
            {
                var descSuffix = string.IsNullOrEmpty(n.Goal) ? "" : $" — {n.Goal}";
                var chapterTag = string.IsNullOrEmpty(n.NodeChapterId) ? "" : " [已有章节]";
                sb.AppendLine($"- {n.Title}{descSuffix}{chapterTag}");
            }
            sb.AppendLine();
        }

        if (bb.RecentChapters.Count > 0)
        {
            sb.AppendLine("## 最近章节");
            foreach (var c in bb.RecentChapters)
                sb.AppendLine($"- 第{c.Sequence}章 {c.Title}（{c.WordCount}字·{c.Status}）{(string.IsNullOrEmpty(c.Summary) ? "" : $"：{c.Summary}")}");
            sb.AppendLine();
        }

        if (bb.Foreshadowings.Pending.Count > 0 || bb.Foreshadowings.Hinted.Count > 0)
        {
            sb.AppendLine("## 伏笔追踪");
            if (bb.Foreshadowings.Pending.Count > 0)
            {
                sb.AppendLine("### 待回收");
                foreach (var f in bb.Foreshadowings.Pending)
                {
                    var overdueTag = bb.Foreshadowings.OverdueCount > 0 && f.SetupChapterSequence > 0 ? " ⚠" : "";
                    sb.AppendLine($"- [{f.Id}] {f.Title}（重要性:{f.Importance}·埋设于第{f.SetupChapterSequence}章）{overdueTag}");
                    if (!string.IsNullOrEmpty(f.Description))
                        sb.AppendLine($"  > {f.Description}");
                }
            }
            if (bb.Foreshadowings.Hinted.Count > 0)
            {
                sb.AppendLine("### 已暗示待回收");
                foreach (var f in bb.Foreshadowings.Hinted)
                    sb.AppendLine($"- [{f.Id}] {f.Title}（重要性:{f.Importance}）");
            }
            if (bb.Foreshadowings.OverdueCount > 0)
                sb.AppendLine($"⚠ 有 {bb.Foreshadowings.OverdueCount} 个伏笔已超过5章未回收，请优先处理。");
            sb.AppendLine();
        }

        if (bb.TimelineEvents.Count > 0)
        {
            sb.AppendLine("## 故事时间线");
            foreach (var t in bb.TimelineEvents)
            {
                var chapterTag = t.ChapterSequence > 0 ? $"[第{t.ChapterSequence}章]" : "";
                sb.AppendLine($"- {t.EventTime:yyyy-MM-dd} [{t.EventType}] {t.Title} {chapterTag}");
                if (!string.IsNullOrEmpty(t.Description))
                    sb.AppendLine($"  > {t.Description}");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
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
