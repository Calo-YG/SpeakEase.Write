using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

public sealed class CreationSessionManager : ICreationSessionManager
{
    private const int MaxTurnsBeforeArchive = 10;
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromHours(24);

    private readonly SpeakEaseDbContext _db;
    private readonly ISnowflakeIdGenerator _idGen;
    private readonly IUserContext _user;
    private readonly ILogger<CreationSessionManager> _log;

    public CreationSessionManager(
        SpeakEaseDbContext db,
        ISnowflakeIdGenerator idGen,
        IUserContext user,
        ILogger<CreationSessionManager> log)
    {
        _db = db;
        _idGen = idGen;
        _user = user;
        _log = log;
    }

    public async Task<ApiResult<CreationSessionDto>> StartSessionAsync(string workId)
    {
        var userId = _user.UserId;

        await _db.AICreationSessions
            .Where(s => s.WorkId == workId && s.UserId == userId
                        && (s.Status == "active" || s.Status == "paused"))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "closed")
                .SetProperty(x => x.CloseReason, "被新会话取代")
                .SetProperty(x => x.UpdateAt, DateTime.UtcNow)
                .SetProperty(x => x.UpdateBy, userId));

        var now = DateTime.UtcNow;
        var entity = new AICreationSessionEntity
        {
            Id = _idGen.NextIdString(),
            UserId = userId,
            WorkId = workId,
            Status = "active",
            TurnCount = 0,
            ContextSnapshotJson = "{}",
            AdoptedContentJson = "[]",
            MessagesJson = "[]",
            StartedAt = now,
            LastActivityAt = now,
            ExpiresAt = now.Add(InactivityTimeout),
            CreateBy = userId,
            UpdateBy = userId
        };

        _db.AICreationSessions.Add(entity);
        await _db.SaveChangesAsync();

        _log.LogInformation("用户 {UserId} 在作品 {WorkId} 启动会话 {SessionId}", userId, workId, entity.Id);

        return new ApiResult<CreationSessionDto>(MapToDto(entity));
    }

    public async Task<ApiResult<CreationSessionDto>> RecordTurnAsync(string sessionId)
    {
        var session = await FindOwnedSessionAsync(sessionId);
        if (session == null) return new ApiResult<CreationSessionDto>("会话不存在或无权访问", 404);
        if (session.Status != "active") return new ApiResult<CreationSessionDto>("会话已结束，无法继续对话", 400);

        session.TurnCount++;
        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = session.LastActivityAt.Add(InactivityTimeout);
        session.UpdateAt = session.LastActivityAt;
        session.UpdateBy = _user.UserId;

        if (session.TurnCount == MaxTurnsBeforeArchive + 1)
            session.MessagesJson = "[]";

        await _db.SaveChangesAsync();
        return new ApiResult<CreationSessionDto>(MapToDto(session));
    }

    public async Task<ApiResult> AdoptContentAsync(string sessionId, AdoptContentRequest request)
    {
        var session = await FindOwnedSessionAsync(sessionId);
        if (session == null) return new ApiResult("会话不存在或无权访问", 404);
        if (session.Status != "active") return new ApiResult("会话已结束，无法采纳内容", 400);

        var adopted = DeserializeAdopted(session.AdoptedContentJson);
        adopted.Add(new AdoptedItem
        {
            TurnNumber = session.TurnCount,
            Content = request.Content,
            Summary = request.Summary ?? string.Empty,
            AdoptedAt = DateTime.UtcNow
        });
        session.AdoptedContentJson = JsonHelper.Serialize(adopted);
        session.LastActivityAt = DateTime.UtcNow;
        session.UpdateAt = session.LastActivityAt;
        session.UpdateBy = _user.UserId;

        await _db.SaveChangesAsync();
        _log.LogInformation("会话 {SessionId} 第 {Turn} 轮内容已采纳", sessionId, session.TurnCount);

        return new ApiResult(true);
    }

    public async Task<ApiResult<CreationSessionDto>> PauseSessionAsync(string sessionId)
    {
        var session = await FindOwnedSessionAsync(sessionId);
        if (session == null) return new ApiResult<CreationSessionDto>("会话不存在或无权访问", 404);
        if (session.Status != "active") return new ApiResult<CreationSessionDto>("只有活跃会话才能暂停", 400);

        session.Status = "paused";
        session.UpdateAt = DateTime.UtcNow;
        session.UpdateBy = _user.UserId;
        await _db.SaveChangesAsync();

        return new ApiResult<CreationSessionDto>(MapToDto(session));
    }

    public async Task<ApiResult> CancelSessionAsync(string sessionId)
    {
        var session = await FindOwnedSessionAsync(sessionId);
        if (session == null) return new ApiResult("会话不存在或无权访问", 404);
        if (session.Status == "cancelled") return new ApiResult("会话已取消", 400);

        session.Status = "cancelled";
        session.CloseReason = "用户取消";
        session.ContextSnapshotJson = "{}";
        session.MessagesJson = "[]";
        session.UpdateAt = DateTime.UtcNow;
        session.UpdateBy = _user.UserId;
        await _db.SaveChangesAsync();

        _log.LogInformation("会话 {SessionId} 已取消", sessionId);
        return new ApiResult(true);
    }

    public async Task<ApiResult<CreationSessionDto>> ResumeSessionAsync(string sessionId)
    {
        var session = await FindOwnedSessionAsync(sessionId);
        if (session == null) return new ApiResult<CreationSessionDto>("会话不存在或无权访问", 404);

        if (session.Status == "expired")
        {
            session.ContextSnapshotJson = "{}";
            session.MessagesJson = "[]";
            session.TurnCount = 0;
        }
        else if (session.Status != "paused" && session.Status != "closed")
        {
            return new ApiResult<CreationSessionDto>("只有已暂停或已关闭的会话可以恢复", 400);
        }

        session.Status = "active";
        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = session.LastActivityAt.Add(InactivityTimeout);
        session.CloseReason = string.Empty;
        session.UpdateAt = session.LastActivityAt;
        session.UpdateBy = _user.UserId;
        await _db.SaveChangesAsync();

        return new ApiResult<CreationSessionDto>(MapToDto(session));
    }

    public async Task<ApiResult> RollbackToTurnAsync(string sessionId, int targetTurn)
    {
        var session = await FindOwnedSessionAsync(sessionId);
        if (session == null) return new ApiResult("会话不存在或无权访问", 404);
        if (session.Status != "active") return new ApiResult("只有活跃会话才能回滚", 400);
        if (targetTurn < 1 || targetTurn >= session.TurnCount)
            return new ApiResult($"目标轮次 {targetTurn} 无效", 400);

        session.TurnCount = targetTurn;
        session.MessagesJson = "[]";
        session.ContextSnapshotJson = "{}";
        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = session.LastActivityAt.Add(InactivityTimeout);
        session.UpdateAt = session.LastActivityAt;
        session.UpdateBy = _user.UserId;
        await _db.SaveChangesAsync();

        return new ApiResult(true);
    }

    public async Task<ApiResult<CreationSessionDto>> GetActiveSessionAsync(string workId)
    {
        var session = await _db.AICreationSessions
            .AsNoTracking()
            .Where(s => s.WorkId == workId && s.UserId == _user.UserId && s.Status == "active")
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        if (session == null)
            return new ApiResult<CreationSessionDto>("当前作品无活跃会话", 404);

        return new ApiResult<CreationSessionDto>(MapToDto(session));
    }

    public async Task<ApiResult<List<CreationSessionDto>>> ListSessionsAsync(string workId)
    {
        var sessions = await _db.AICreationSessions
            .AsNoTracking()
            .Where(s => s.WorkId == workId && s.UserId == _user.UserId)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new CreationSessionDto
            {
                SessionId = s.Id,
                WorkId = s.WorkId,
                Status = s.Status,
                TurnCount = s.TurnCount,
                StartedAt = s.StartedAt,
                LastActivityAt = s.LastActivityAt,
                ExpiresAt = s.ExpiresAt,
                CloseReason = s.CloseReason
            })
            .ToListAsync();

        return new ApiResult<List<CreationSessionDto>>(sessions);
    }

    public async Task<int> ExpireStaleSessionsAsync()
    {
        var now = DateTime.UtcNow;
        var count = await _db.AICreationSessions
            .Where(s => (s.Status == "active" || s.Status == "paused")
                        && s.LastActivityAt < now.Add(-InactivityTimeout))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "expired")
                .SetProperty(x => x.CloseReason, "超时自动过期")
                .SetProperty(x => x.UpdateAt, now));

        if (count > 0)
            _log.LogInformation("已过期 {Count} 个超时会话", count);

        return count;
    }

    private async Task<AICreationSessionEntity> FindOwnedSessionAsync(string sessionId)
    {
        return await _db.AICreationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == _user.UserId);
    }

    private static List<AdoptedItem> DeserializeAdopted(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return new();
        try { return JsonHelper.Deserialize<List<AdoptedItem>>(json) ?? new(); }
        catch { return new(); }
    }

    private static CreationSessionDto MapToDto(AICreationSessionEntity entity)
        => new CreationSessionDto
        {
            SessionId = entity.Id,
            WorkId = entity.WorkId,
            Status = entity.Status,
            TurnCount = entity.TurnCount,
            StartedAt = entity.StartedAt,
            LastActivityAt = entity.LastActivityAt,
            ExpiresAt = entity.ExpiresAt,
            CloseReason = entity.CloseReason
        };
}
