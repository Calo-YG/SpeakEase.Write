using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

public class CreationSessionManager(
    SpeakEaseDbContext db,
    ILogger<CreationSessionManager> logger,
    IUserContext userContext) : ICreationSessionManager
{
    private const int MaxTurnsBeforeArchive = 10;
    private static readonly TimeSpan SessionExpiration = TimeSpan.FromHours(24);

    public async Task<ApiResult<CreationSessionDto>> StartSessionAsync(string workId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<CreationSessionDto>("用户未登录。", 401);

        await CloseActiveSessionForWorkAsync(workId, userId, "cancelled", "new_session_started");

        var entity = new AICreationSessionEntity
        {
            WorkId = workId,
            UserId = userId,
            Status = "active",
            TurnCount = 0,
            AdoptedContentJson = "[]",
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(SessionExpiration),
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
        session.LastActivityAt = DateTime.UtcNow;

        if (session.TurnCount % MaxTurnsBeforeArchive == 0)
        {
            logger.LogInformation("session {SessionId} reached {TurnCount} turns, performing archive check", sessionId, session.TurnCount);
            await PerformArchiveAsync(session, userId);
        }

        session.ExpiresAt = DateTime.UtcNow.Add(SessionExpiration);
        await db.SaveChangesAsync();
        return MapToResult(session);
    }

    private async Task PerformArchiveAsync(AICreationSessionEntity currentSession, string userId)
    {
        if (currentSession.TurnCount >= MaxTurnsBeforeArchive * 2)
        {
            await CloseActiveSessionForWorkAsync(currentSession.WorkId, userId, "expired", "archive_turns_limit");
            var entity = new AICreationSessionEntity
            {
                WorkId = currentSession.WorkId,
                UserId = userId,
                Status = "active",
                TurnCount = 0,
                AdoptedContentJson = "[]",
                StartedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(SessionExpiration),
            };
            db.AICreationSessions.Add(entity);
            logger.LogInformation("session archived for work {WorkId}, new session created", currentSession.WorkId);
        }
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
            AdoptedAt = DateTime.UtcNow,
        });

        session.AdoptedContentJson = JsonHelper.Serialize(adopted);
        session.LastActivityAt = DateTime.UtcNow;
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
        session.LastActivityAt = DateTime.UtcNow;
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
        session.LastActivityAt = DateTime.UtcNow;
        session.AdoptedContentJson = "[]";

        await db.SaveChangesAsync();
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
        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow.Add(SessionExpiration);
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
        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow.Add(SessionExpiration);
        await db.SaveChangesAsync();

        return new ApiResult(true);
    }

    public async Task<ApiResult<CreationSessionDto>> GetActiveSessionAsync(string workId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<CreationSessionDto>("用户未登录。", 401);

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
            .Where(x => x.Status == "active" && x.ExpiresAt.HasValue && x.ExpiresAt < DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "expired")
                .SetProperty(x => x.CloseReason, "expired_timeout")
                .SetProperty(x => x.LastActivityAt, DateTime.UtcNow));
    }

    public async Task SaveMessagesAsync(string sessionId, int turnNumber, string userMessage, string aiMessage, List<(string ToolName, bool Success, string Content)> toolResults = null)
    {
        var now = DateTime.UtcNow;
        var messages = new List<AICreationMessageEntity>
        {
            new()
            {
                SessionId = sessionId,
                Role = "user",
                Content = userMessage,
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
                    SessionId = sessionId,
                    Role = "tool",
                    Content = content,
                    TurnNumber = turnNumber,
                    ToolName = toolName,
                    ToolSuccess = success,
                    CreatedAt = now,
                });
            }
        }

        messages.Add(new AICreationMessageEntity
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = aiMessage,
            TurnNumber = turnNumber,
            CreatedAt = now,
        });

        db.AICreationMessages.AddRange(messages);
        await db.SaveChangesAsync();
    }

    public async Task<ApiResult<List<SessionMessageResponse>>> GetSessionMessagesAsync(string sessionId, int? limit = null)
    {
        var query = db.AICreationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.TurnNumber);

        var messages = limit.HasValue
            ? await query.Take(limit.Value).ToListAsync()
            : await query.ToListAsync();

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

    private async Task CloseActiveSessionForWorkAsync(string workId, string userId, string status, string reason)
    {
        var active = await db.AICreationSessions
            .Where(x => x.WorkId == workId && x.UserId == userId && x.Status == "active")
            .ToListAsync();

        foreach (var s in active)
        {
            s.Status = status;
            s.CloseReason = reason;
            s.LastActivityAt = DateTime.UtcNow;
        }
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
