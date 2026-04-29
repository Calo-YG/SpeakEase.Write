using Microsoft.Extensions.Logging;
using SpeakEase.Write.Application.Contracts.Snapshot;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Application.Applications;

public sealed class BlackboardUpdater : IBlackboardUpdater
{
    private readonly BlackboardHolder _holder;
    private readonly ILogger<BlackboardUpdater> _log;

    public BlackboardUpdater(BlackboardHolder holder, ILogger<BlackboardUpdater> log)
    {
        _holder = holder;
        _log = log;
    }

    public void UpdateChapterContent(string chapterId, string content, string summary)
    {
        var board = _holder.Blackboard;
        if (board == null)
        {
            _log.LogDebug("黑板为空，跳过章节 {ChapterId} 增量更新", chapterId);
            return;
        }

        var target = board.RecentChapters.FirstOrDefault(c => c.ChapterId == chapterId);
        if (target != null)
        {
            target.Content = content ?? string.Empty;
            target.Summary = summary ?? target.Summary;
            _log.LogDebug("黑板章节 {ChapterId} 已增量更新", chapterId);
        }
        else
        {
            _log.LogDebug("黑板中未找到章节 {ChapterId}，跳过增量更新", chapterId);
        }
    }

    public void RemoveChapter(string chapterId)
    {
        var board = _holder.Blackboard;
        if (board == null) return;

        board.RecentChapters.RemoveAll(c => c.ChapterId == chapterId);
        _log.LogDebug("黑板章节 {ChapterId} 已移除", chapterId);
    }

    public void Clear()
    {
        _holder.Blackboard = null;
    }
}
