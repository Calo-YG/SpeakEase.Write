using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.AI.Memory;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

public class CreationSessionManager(
    SpeakEaseDbContext db,
    ILogger<CreationSessionManager> logger,
    IUserContext userContext,
    IMemoryProvider memory,
    ISnowflakeIdGenerator snowflakeIdGenerator) : ICreationSessionManager
{
    private const int MaxTurnsBeforeArchive = 10;

    private static readonly TimeSpan SessionExpiration = TimeSpan.FromHours(24);

    public async Task<ApiResult<CreationSessionDto>> StartSessionAsync(string workId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<CreationSessionDto>("用户未登录。", 401);

        if (string.IsNullOrWhiteSpace(workId))
            return new ApiResult<CreationSessionDto>("作品标识不能为空。", 400);

        if (!await OwnsWorkAsync(workId, userId))
            return new ApiResult<CreationSessionDto>("作品不存在或无权访问。", 404);

        await CloseActiveSessionForWorkAsync(workId, userId, "cancelled", "new_session_started");

        var entity = new AICreationSessionEntity
        {
            Id = snowflakeIdGenerator.NextIdString(),
            WorkId = workId,
            UserId = userId,
            Status = "active",
            TurnCount = 0,
            AdoptedContentJson = "[]",
            StartedAt = DateTime.Now,
            LastActivityAt = DateTime.Now,
            ExpiresAt = DateTime.Now.Add(SessionExpiration),
        };

        db.AICreationSessions.Add(entity);
        await db.SaveChangesAsync();
        return MapToResult(entity);
    }

    public async Task<ApiResult<CreationSessionDto>> RecordTurnAsync(string sessionId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<CreationSessionDto>("用户未登录。", 401);

        var session = await GetOwnedSessionAsync(sessionId, userId);
        if (session is null)
            return new ApiResult<CreationSessionDto>("会话不存在。", 404);

        session.TurnCount++;
        session.LastActivityAt = DateTime.Now;
        session.ExpiresAt = DateTime.Now.Add(SessionExpiration);

        await db.SaveChangesAsync();

        if (session.TurnCount % MaxTurnsBeforeArchive == 0)
        {
            logger.LogInformation("session {SessionId} reached {TurnCount} turns, performing archive check", sessionId, session.TurnCount);
            var archived = await PerformArchiveAsync(session, userId);
            if (archived)
            {
                return await GetActiveSessionAfterArchiveAsync(session.WorkId, userId);
            }
        }

        return MapToResult(session);
    }

    public async Task<ApiResult<CreationSessionDto>> AppendTurnAsync(
        string sessionId,
        string userMessage,
        string aiMessage,
        List<(string ToolName, bool Success, string Content)> toolResults = null,
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<CreationSessionDto>("User is not signed in.", 401);

        if (string.IsNullOrWhiteSpace(sessionId))
            return new ApiResult<CreationSessionDto>("Session id is required.", 400);

        var session = await GetOwnedSessionAsync(sessionId, userId);
        if (session is null)
            return new ApiResult<CreationSessionDto>("Session does not exist.", 404);

        if (session.Status != "active")
            return new ApiResult<CreationSessionDto>("Session is not active.", 400);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        session.TurnCount++;
        session.LastActivityAt = DateTime.Now;
        session.ExpiresAt = DateTime.Now.Add(SessionExpiration);

        db.AICreationMessages.AddRange(BuildTurnMessages(
            sessionId,
            session.TurnCount,
            userMessage,
            aiMessage,
            toolResults));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await memory.RefreshAfterTurnAsync(userId, session.WorkId, sessionId, session.TurnCount, cancellationToken);

        if (session.TurnCount % MaxTurnsBeforeArchive == 0)
        {
            logger.LogInformation("session {SessionId} reached {TurnCount} turns, performing archive check", sessionId, session.TurnCount);
            var archived = await PerformArchiveAsync(session, userId);
            if (archived)
                return await GetActiveSessionAfterArchiveAsync(session.WorkId, userId);
        }

        return MapToResult(session);
    }

    private async Task<bool> PerformArchiveAsync(AICreationSessionEntity currentSession, string userId)
    {
        if (currentSession.TurnCount >= MaxTurnsBeforeArchive * 2)
        {
            await CloseActiveSessionForWorkAsync(currentSession.WorkId, userId, "expired", "archive_turns_limit");
            var entity = new AICreationSessionEntity
            {
                Id = snowflakeIdGenerator.NextIdString(),
                WorkId = currentSession.WorkId,
                UserId = userId,
                Status = "active",
                TurnCount = 0,
                AdoptedContentJson = "[]",
                StartedAt = DateTime.Now,
                LastActivityAt = DateTime.Now,
                ExpiresAt = DateTime.Now.Add(SessionExpiration),
            };
            db.AICreationSessions.Add(entity);
            await db.SaveChangesAsync();
            logger.LogInformation("session archived for work {WorkId}, new session created", currentSession.WorkId);
            return true;
        }

        return false;
    }

    private async Task<ApiResult<CreationSessionDto>> GetActiveSessionAfterArchiveAsync(string workId, string userId)
    {
        var newSession = await db.AICreationSessions
            .AsNoTracking()
            .Where(x => x.WorkId == workId && x.UserId == userId && x.Status == "active")
            .OrderByDescending(x => x.LastActivityAt)
            .FirstOrDefaultAsync();

        return newSession is null
            ? new ApiResult<CreationSessionDto>("归档后未找到新会话。", 500)
            : MapToResult(newSession);
    }

    public async Task<ApiResult> AdoptContentAsync(string sessionId, AdoptContentRequest request)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult("用户未登录。", 401);

        var session = await GetOwnedSessionAsync(sessionId, userId);
        if (session is null)
            return new ApiResult("会话不存在。", 404);

        var adopted = DeserializeAdopted(session.AdoptedContentJson);
        adopted.Add(new AdoptedItem
        {
            TurnNumber = session.TurnCount,
            Content = request.Content,
            Summary = request.Summary,
            AdoptedAt = DateTime.Now,
        });

        session.AdoptedContentJson = JsonHelper.Serialize(adopted);
        session.LastActivityAt = DateTime.Now;
        await db.SaveChangesAsync();
        return new ApiResult(true);
    }

    public async Task<ApiResult<CreationSessionDto>> PauseSessionAsync(string sessionId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<CreationSessionDto>("用户未登录。", 401);

        var session = await GetOwnedSessionAsync(sessionId, userId);
        if (session is null)
            return new ApiResult<CreationSessionDto>("会话不存在。", 404);
        if (session.Status != "active")
            return new ApiResult<CreationSessionDto>("会话不在活跃状态。", 400);

        session.Status = "paused";
        session.LastActivityAt = DateTime.Now;
        await db.SaveChangesAsync();
        return MapToResult(session);
    }

    public async Task<ApiResult> CancelSessionAsync(string sessionId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult("用户未登录。", 401);

        var session = await GetOwnedSessionAsync(sessionId, userId);
        if (session is null)
            return new ApiResult("会话不存在。", 404);

        session.Status = "cancelled";
        session.CloseReason = "user_cancelled";
        session.LastActivityAt = DateTime.Now;
        session.AdoptedContentJson = "[]";

        await db.SaveChangesAsync();
        await memory.InvalidateSessionAsync(userId, session.WorkId, sessionId);
        return new ApiResult(true);
    }

    public async Task<ApiResult<CreationSessionDto>> ResumeSessionAsync(string sessionId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<CreationSessionDto>("用户未登录。", 401);

        var session = await GetOwnedSessionAsync(sessionId, userId);
        if (session is null)
            return new ApiResult<CreationSessionDto>("会话不存在。", 404);
        if (session.Status != "paused")
            return new ApiResult<CreationSessionDto>("会话未处于暂停状态。", 400);

        session.Status = "active";
        session.LastActivityAt = DateTime.Now;
        session.ExpiresAt = DateTime.Now.Add(SessionExpiration);
        await db.SaveChangesAsync();
        return MapToResult(session);
    }

    public async Task<ApiResult> RollbackToTurnAsync(string sessionId, int targetTurn)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult("用户未登录。", 401);

        var session = await GetOwnedSessionAsync(sessionId, userId);
        if (session is null)
            return new ApiResult("会话不存在。", 404);

        if (targetTurn < 0 || targetTurn > session.TurnCount)
            return new ApiResult("无效的目标轮次。", 400);

        var adopted = DeserializeAdopted(session.AdoptedContentJson);
        adopted.RemoveAll(a => a.TurnNumber > targetTurn);
        session.AdoptedContentJson = JsonHelper.Serialize(adopted);

        await db.AICreationMessages
            .Where(m => m.SessionId == sessionId && m.TurnNumber > targetTurn)
            .ExecuteDeleteAsync();

        session.TurnCount = targetTurn;
        session.LastActivityAt = DateTime.Now;
        session.ExpiresAt = DateTime.Now.Add(SessionExpiration);
        await db.SaveChangesAsync();
        await memory.RefreshAfterTurnAsync(userId, session.WorkId, sessionId, targetTurn);

        return new ApiResult(true);
    }

    public async Task<ApiResult<CreationSessionDto>> GetActiveSessionAsync(string workId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<CreationSessionDto>("用户未登录。", 401);

        if (string.IsNullOrWhiteSpace(workId))
            return new ApiResult<CreationSessionDto>("作品标识不能为空。", 400);

        if (!await OwnsWorkAsync(workId, userId))
            return new ApiResult<CreationSessionDto>("作品不存在或无权访问。", 404);

        var session = await db.AICreationSessions
            .AsNoTracking()
            .Where(x => x.WorkId == workId && x.UserId == userId && x.Status == "active")
            .OrderByDescending(x => x.LastActivityAt)
            .FirstOrDefaultAsync();

        return session is null
            ? new ApiResult<CreationSessionDto>("没有活跃会话。", 404)
            : MapToResult(session);
    }

    public async Task<ApiResult<List<CreationSessionDto>>> ListSessionsAsync(string workId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<List<CreationSessionDto>>("用户未登录。", 401);

        if (string.IsNullOrWhiteSpace(workId))
            return new ApiResult<List<CreationSessionDto>>("作品标识不能为空。", 400);

        if (!await OwnsWorkAsync(workId, userId))
            return new ApiResult<List<CreationSessionDto>>("作品不存在或无权访问。", 404);

        var sessions = await db.AICreationSessions
            .AsNoTracking()
            .Where(x => x.WorkId == workId && x.UserId == userId)
            .OrderByDescending(x => x.StartedAt)
            .Select(x => new CreationSessionDto
            {
                SessionId = x.Id,
                WorkId = x.WorkId,
                Status = x.Status,
                TurnCount = x.TurnCount,
                StartedAt = x.StartedAt,
                LastActivityAt = x.LastActivityAt,
                ExpiresAt = x.ExpiresAt,
                CloseReason = x.CloseReason,
            })
            .ToListAsync();

        return new ApiResult<List<CreationSessionDto>>(sessions);
    }

    public async Task<int> ExpireStaleSessionsAsync()
    {
        return await db.AICreationSessions
            .Where(x => x.Status == "active" && x.ExpiresAt.HasValue && x.ExpiresAt < DateTime.Now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "expired")
                .SetProperty(x => x.CloseReason, "expired_timeout")
                .SetProperty(x => x.LastActivityAt, DateTime.Now));
    }

    public async Task SaveMessagesAsync(string sessionId, int turnNumber, string userMessage, string aiMessage, List<(string ToolName, bool Success, string Content)> toolResults = null)
    {
        db.AICreationMessages.AddRange(BuildTurnMessages(sessionId, turnNumber, userMessage, aiMessage, toolResults));
        await db.SaveChangesAsync();
    }

    private List<AICreationMessageEntity> BuildTurnMessages(
        string sessionId,
        int turnNumber,
        string userMessage,
        string aiMessage,
        List<(string ToolName, bool Success, string Content)> toolResults)
    {
        var now = DateTime.Now;
        var messages = new List<AICreationMessageEntity>
        {
            new()
            {
                Id = snowflakeIdGenerator.NextIdString(),
                SessionId = sessionId,
                Role = "user",
                Content = userMessage ?? string.Empty,
                TurnNumber = turnNumber,
                CreatedAt = now,
            }
        };

        if (toolResults is { Count: > 0 })
        {
            foreach (var (toolName, success, content) in toolResults)
            {
                messages.Add(new AICreationMessageEntity
                {
                    Id = snowflakeIdGenerator.NextIdString(),
                    SessionId = sessionId,
                    Role = "tool",
                    Content = content ?? string.Empty,
                    TurnNumber = turnNumber,
                    ToolName = toolName ?? "tool",
                    ToolSuccess = success,
                    CreatedAt = now,
                });
            }
        }

        messages.Add(new AICreationMessageEntity
        {
            Id = snowflakeIdGenerator.NextIdString(),
            SessionId = sessionId,
            Role = "assistant",
            Content = aiMessage ?? string.Empty,
            TurnNumber = turnNumber,
            CreatedAt = now,
        });

        return messages;
    }

    public async Task<ApiResult<List<SessionMessageResponse>>> GetSessionMessagesAsync(string sessionId, int? limit = null)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<List<SessionMessageResponse>>("用户未登录。", 401);

        if (string.IsNullOrWhiteSpace(sessionId))
            return new ApiResult<List<SessionMessageResponse>>("会话标识不能为空。", 400);

        var ownsSession = await db.AICreationSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.UserId == userId);
        if (!ownsSession)
            return new ApiResult<List<SessionMessageResponse>>("会话不存在或无权访问。", 404);

        var take = limit.HasValue
            ? Math.Clamp(limit.Value, 1, 200)
            : 200;

        var query = db.AICreationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.TurnNumber);

        var messages = await query.Take(take).ToListAsync();

        var result = messages.Select(m => new SessionMessageResponse
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            TurnNumber = m.TurnNumber,
            ToolName = m.ToolName,
            ToolSuccess = m.ToolSuccess,
            CreatedAt = m.CreatedAt,
        }).ToList();

        return new ApiResult<List<SessionMessageResponse>>(result);
    }

    private async Task<AICreationSessionEntity> GetOwnedSessionAsync(string sessionId, string userId)
    {
        var session = await db.AICreationSessions.FindAsync(sessionId);
        if (session is null || session.UserId != userId) return null;
        return session;
    }

    private async Task<bool> OwnsWorkAsync(string workId, string userId)
    {
        return await db.Works
            .AsNoTracking()
            .AnyAsync(x => x.Id == workId && x.UserId == userId);
    }

    private async Task CloseActiveSessionForWorkAsync(string workId, string userId, string status, string reason)
    {
        await db.AICreationSessions
            .Where(x => x.WorkId == workId && x.UserId == userId && x.Status == "active")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.CloseReason, reason)
                .SetProperty(x => x.LastActivityAt, DateTime.Now));
    }

    private static List<AdoptedItem> DeserializeAdopted(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<AdoptedItem>();
        return JsonHelper.Deserialize<List<AdoptedItem>>(json) ?? new List<AdoptedItem>();
    }

    private static ApiResult<CreationSessionDto> MapToResult(AICreationSessionEntity entity)
    {
        return new ApiResult<CreationSessionDto>(new CreationSessionDto
        {
            SessionId = entity.Id,
            WorkId = entity.WorkId,
            Status = entity.Status,
            TurnCount = entity.TurnCount,
            StartedAt = entity.StartedAt,
            LastActivityAt = entity.LastActivityAt,
            ExpiresAt = entity.ExpiresAt,
            CloseReason = entity.CloseReason,
        });
    }
}
