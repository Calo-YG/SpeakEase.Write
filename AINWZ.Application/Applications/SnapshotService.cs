using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Snapshot;
using SpeakEase.Write.Application.Contracts.Snapshot.Dto;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

public sealed class SnapshotService : ISnapshotService
{
    private const int MaxSnapshotsPerWork = 50;
    private static readonly TimeSpan ArchiveThreshold = TimeSpan.FromDays(7);

    private readonly SpeakEaseDbContext _db;
    private readonly ISnowflakeIdGenerator _idGen;
    private readonly IUserContext _user;
    private readonly BlackboardHolder _blackboardHolder;
    private readonly ILogger<SnapshotService> _log;

    public SnapshotService(
        SpeakEaseDbContext db,
        ISnowflakeIdGenerator idGen,
        IUserContext user,
        BlackboardHolder blackboardHolder,
        ILogger<SnapshotService> log)
    {
        _db = db;
        _idGen = idGen;
        _user = user;
        _blackboardHolder = blackboardHolder;
        _log = log;
    }

    public async Task<string> CaptureBeforeSnapshotAsync(string workId, string correlationId)
    {
        var blackboard = _blackboardHolder.Blackboard;
        if (blackboard == null)
        {
            _log.LogWarning("尝试保存 Before 快照但黑板为空，WorkId={WorkId}", workId);
            return string.Empty;
        }

        var entity = new MemorySnapshotEntity
        {
            Id = _idGen.NextIdString(),
            UserId = _user.UserId,
            WorkId = workId,
            ChapterId = string.Empty,
            SnapshotType = "before",
            SnapshotJson = JsonHelper.Serialize(blackboard),
            Summary = $"Agent 执行前快照 - {correlationId}",
            VersionId = correlationId,
            CreateBy = _user.UserId,
            UpdateBy = _user.UserId
        };

        _db.MemorySnapshots.Add(entity);
        await EnforceRetentionAsync(workId);
        await _db.SaveChangesAsync();

        _log.LogDebug("Before 快照已保存：{SnapshotId}（{CorrelationId}）", entity.Id, correlationId);

        return entity.Id;
    }

    public async Task<string> CaptureAfterSnapshotAsync(string workId, string correlationId)
    {
        var blackboard = _blackboardHolder.Blackboard;
        if (blackboard == null)
        {
            _log.LogWarning("尝试保存 After 快照但黑板为空，WorkId={WorkId}", workId);
            return string.Empty;
        }

        var entity = new MemorySnapshotEntity
        {
            Id = _idGen.NextIdString(),
            UserId = _user.UserId,
            WorkId = workId,
            ChapterId = string.Empty,
            SnapshotType = "after",
            SnapshotJson = JsonHelper.Serialize(blackboard),
            Summary = $"Agent 执行后快照 - {correlationId}",
            VersionId = correlationId,
            CreateBy = _user.UserId,
            UpdateBy = _user.UserId
        };

        _db.MemorySnapshots.Add(entity);
        await _db.SaveChangesAsync();

        _log.LogDebug("After 快照已保存：{SnapshotId}（{CorrelationId}）", entity.Id, correlationId);

        return entity.Id;
    }

    public async Task<ApiResult<bool>> RestoreSnapshotAsync(string snapshotId)
    {
        var snapshot = await _db.MemorySnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.UserId == _user.UserId);

        if (snapshot == null)
            return new ApiResult<bool>("快照不存在", 404);

        if (string.IsNullOrWhiteSpace(snapshot.SnapshotJson) || snapshot.SnapshotJson == "{}")
            return new ApiResult<bool>("快照数据为空，无法恢复", 400);

        try
        {
            var blackboard = JsonHelper.Deserialize<WritingBlackboard>(snapshot.SnapshotJson);
            if (blackboard == null)
                return new ApiResult<bool>("快照数据反序列化失败", 500);

            _blackboardHolder.Blackboard = blackboard;

            _log.LogInformation("黑板已从快照 {SnapshotId} 恢复（{SnapshotType}）", snapshotId, snapshot.SnapshotType);

            return new ApiResult<bool>(true);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "从快照 {SnapshotId} 恢复黑板失败", snapshotId);
            return new ApiResult<bool>("快照恢复失败", 500);
        }
    }

    public async Task<ApiResult<bool>> RestoreLastAfterSnapshotAsync(string workId)
    {
        var snapshot = await _db.MemorySnapshots
            .AsNoTracking()
            .Where(s => s.WorkId == workId && s.UserId == _user.UserId && s.SnapshotType == "after")
            .OrderByDescending(s => s.CreateAt)
            .FirstOrDefaultAsync();

        if (snapshot == null)
            return new ApiResult<bool>("未找到 After 快照", 404);

        return await RestoreSnapshotAsync(snapshot.Id);
    }

    public async Task<ApiResult<bool>> UndoLastAgentRunAsync(string workId)
    {
        var snapshot = await _db.MemorySnapshots
            .AsNoTracking()
            .Where(s => s.WorkId == workId && s.UserId == _user.UserId && s.SnapshotType == "before")
            .OrderByDescending(s => s.CreateAt)
            .FirstOrDefaultAsync();

        if (snapshot == null)
            return new ApiResult<bool>("未找到可回退的快照", 404);

        return await RestoreSnapshotAsync(snapshot.Id);
    }

    public async Task<ApiResult<List<SnapshotSummaryDto>>> ListSnapshotsAsync(string workId)
    {
        var snapshots = await _db.MemorySnapshots
            .AsNoTracking()
            .Where(s => s.WorkId == workId && s.UserId == _user.UserId)
            .OrderByDescending(s => s.CreateAt)
            .Take(MaxSnapshotsPerWork)
            .Select(s => new SnapshotSummaryDto
            {
                SnapshotId = s.Id,
                SnapshotType = s.SnapshotType,
                Summary = s.Summary,
                CreatedAt = s.CreateAt,
                VersionId = s.VersionId
            })
            .ToListAsync();

        return new ApiResult<List<SnapshotSummaryDto>>(snapshots);
    }

    public async Task<int> ArchiveOldSnapshotsAsync()
    {
        var cutoff = DateTime.UtcNow.Add(-ArchiveThreshold);

        var count = await _db.MemorySnapshots
            .Where(s => s.CreateAt < cutoff && !string.IsNullOrWhiteSpace(s.SnapshotJson) && s.SnapshotJson != "{}")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.SnapshotJson, "{}")
                .SetProperty(x => x.Summary, x => $"[已归档] {x.Summary}")
                .SetProperty(x => x.UpdateAt, DateTime.UtcNow));

        if (count > 0)
            _log.LogInformation("已归档 {Count} 个旧快照", count);

        return count;
    }

    private async Task EnforceRetentionAsync(string workId)
    {
        var count = await _db.MemorySnapshots
            .CountAsync(s => s.WorkId == workId && s.UserId == _user.UserId);

        if (count <= MaxSnapshotsPerWork) return;

        var excess = count - MaxSnapshotsPerWork;
        var oldestIds = await _db.MemorySnapshots
            .Where(s => s.WorkId == workId && s.UserId == _user.UserId)
            .OrderBy(s => s.CreateAt)
            .Take(excess)
            .Select(s => s.Id)
            .ToListAsync();

        await _db.MemorySnapshots
            .Where(s => oldestIds.Contains(s.Id))
            .ExecuteDeleteAsync();
    }
}
