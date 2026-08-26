using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEaseDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Applications;

// AI 创作会话管理器：管理创作会话的生命周期（创建、对话轮次、归档、暂停/恢复、回滚）
public class CreationSessionManager(
    SpeakEaseDbContext db,
    ILogger<CreationSessionManager> logger,
    IUserContext userContext,
    IMemoryProvider memory,
    ISnowflakeIdGenerator snowflakeIdGenerator,
    IMemoryRefreshQueue memoryRefreshQueue = null) : ICreationSessionManager
{
    // 每 N 轮对话触发一次归档检查
    private const int MaxTurnsBeforeArchive = 10;

    // 会话过期时间，超时后自动标记为 expired
    private static readonly TimeSpan SessionExpiration = TimeSpan.FromHours(24);

    // 为指定作品启动新的创作会话，同时关闭该作品上已有的活跃会话
    public async Task<ApiResult<CreationSessionDto>> StartSessionAsync(string workId)
    {
        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            return new ApiResult<CreationSessionDto>("用户未登录。", 401);

        if (string.IsNullOrWhiteSpace(workId))
            return new ApiResult<CreationSessionDto>("作品标识不能为空。", 400);

        if (!await OwnsWorkAsync(workId, userId))
            return new ApiResult<CreationSessionDto>("作品不存在或无权访问。", 404);

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // 关闭旧会话和创建新会话必须在同一事务内，避免并发启动留下多个 active 会话。
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
            await transaction.CommitAsync();
            return MapToResult(entity);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            logger.LogWarning(ex, "并发启动作品 {WorkId} 的创作会话失败", workId);
            return new ApiResult<CreationSessionDto>("该作品已有活跃会话，请稍后重试。", 409);
        }
    }

    // 记录一轮对话（轮次+1），达到归档阈值时自动归档并返回新会话
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

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "并发更新创作会话 {SessionId} 轮次失败", sessionId);
            return new ApiResult<CreationSessionDto>("会话已被其他请求更新，请重试。", 409);
        }

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

    // 追加一轮完整对话（用户消息 + AI 消息 + 工具调用结果），在事务中原子完成
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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(ex, "并发追加创作会话 {SessionId} 轮次失败", sessionId);
            return new ApiResult<CreationSessionDto>("会话已被其他请求更新，请重试。", 409);
        }

        try
        {
            if (memoryRefreshQueue is not null)
            {
                await memoryRefreshQueue.EnqueueAsync(new MemoryRefreshRequest
                {
                    UserId = userId,
                    WorkId = session.WorkId,
                    SessionId = sessionId,
                    TurnNumber = session.TurnCount
                }, CancellationToken.None);
            }
            else
            {
                // 兼容未注册队列的测试/旧宿主；生产 DI 会注入后台队列。
                await memory.RefreshAfterTurnAsync(userId, session.WorkId, sessionId, session.TurnCount, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            // 消息事务已经提交，记忆刷新失败只能等待后续队列/重试，不能让 Chat 反向失败。
            logger.LogWarning(
                ex,
                "Memory refresh deferred after turn commit: SessionId={SessionId}, Turn={Turn}",
                sessionId,
                session.TurnCount);
        }

        if (session.TurnCount % MaxTurnsBeforeArchive == 0)
        {
            logger.LogInformation("session {SessionId} reached {TurnCount} turns, performing archive check", sessionId, session.TurnCount);
            var archived = await PerformArchiveAsync(session, userId);
            if (archived)
                return await GetActiveSessionAfterArchiveAsync(session.WorkId, userId);
        }

        return MapToResult(session);
    }

    // 当轮次达到 MaxTurnsBeforeArchive * 2 时归档当前会话并创建新的活跃会话
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

    // 采纳 AI 生成的内容：将当前轮次的内容追加到已采纳列表中
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

    // 暂停会话：将状态从 active 改为 paused
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

    // 取消会话并清除已采纳内容，同时使该会话的内存缓存失效
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

    // 恢复暂停的会话：将状态从 paused 改为 active 并刷新过期时间
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

    // 回滚到指定轮次：删除 targetTurn 之后的所有消息和已采纳内容
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

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await DeleteMessagesAfterTurnAsync(sessionId, targetTurn);

            session.AdoptedContentJson = JsonHelper.Serialize(adopted);
            session.TurnCount = targetTurn;
            session.LastActivityAt = DateTime.Now;
            session.ExpiresAt = DateTime.Now.Add(SessionExpiration);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        try
        {
            // 回滚允许版本下降，先删除旧快照/缓存，再根据剩余消息重建。
            await DeleteSessionMemorySnapshotsAsync(userId, session.WorkId, sessionId);
            await memory.PruneSessionFactsAfterTurnAsync(userId, session.WorkId, sessionId, targetTurn);
            await memory.InvalidateSessionAsync(userId, session.WorkId, sessionId);
            if (memoryRefreshQueue is not null)
            {
                await memoryRefreshQueue.EnqueueAsync(new MemoryRefreshRequest
                {
                    UserId = userId,
                    WorkId = session.WorkId,
                    SessionId = sessionId,
                    TurnNumber = targetTurn
                });
            }
            else
            {
                await memory.RefreshAfterTurnAsync(userId, session.WorkId, sessionId, targetTurn);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Memory refresh deferred after rollback: SessionId={SessionId}, TargetTurn={TargetTurn}",
                sessionId,
                targetTurn);
        }

        return new ApiResult(true);
    }

    private async Task DeleteMessagesAfterTurnAsync(string sessionId, int targetTurn)
    {
        var query = db.AICreationMessages
            .Where(m => m.SessionId == sessionId && m.TurnNumber > targetTurn);
        try
        {
            await query.ExecuteDeleteAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ExecuteDelete", StringComparison.Ordinal))
        {
            var messages = await query.ToListAsync();
            db.AICreationMessages.RemoveRange(messages);
            await db.SaveChangesAsync();
        }
    }

    private async Task DeleteSessionMemorySnapshotsAsync(string userId, string workId, string sessionId)
    {
        var query = db.MemorySnapshots
            .Where(x => x.UserId == userId &&
                        x.WorkId == workId &&
                        x.SessionId == sessionId &&
                        x.SnapshotType == "session-turn-summary");
        try
        {
            await query.ExecuteDeleteAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ExecuteDelete", StringComparison.Ordinal))
        {
            var snapshots = await query.ToListAsync();
            db.MemorySnapshots.RemoveRange(snapshots);
            await db.SaveChangesAsync();
        }
    }

    // 获取作品当前活跃会话（status = active 且最近活跃的）
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

    // 列出作品下所有创作会话，按开始时间倒序
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

    // 批量过期处理：将超时的活跃会话标记为 expired，返回影响行数
    public async Task<int> ExpireStaleSessionsAsync()
    {
        return await db.AICreationSessions
            .Where(x => x.Status == "active" && x.ExpiresAt.HasValue && x.ExpiresAt < DateTime.Now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "expired")
                .SetProperty(x => x.CloseReason, "expired_timeout")
                .SetProperty(x => x.LastActivityAt, DateTime.Now));
    }

    // 保存一轮对话消息记录（用户消息 + 工具结果 + AI 回复）
    public async Task SaveMessagesAsync(string sessionId, int turnNumber, string userMessage, string aiMessage, List<(string ToolName, bool Success, string Content)> toolResults = null)
    {
        db.AICreationMessages.AddRange(BuildTurnMessages(sessionId, turnNumber, userMessage, aiMessage, toolResults));
        await db.SaveChangesAsync();
    }

    // 构建一轮对话的消息实体列表：user → tool(s) → assistant
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

    // 获取会话的消息历史，支持限制返回条数（默认 200，最大 200）
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

        // 限制单次查询最多 200 条消息，防止大数据量查询
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

    // 通过会话 ID 获取会话实体，同时校验归属权
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

    // 使用 ExecuteUpdateAsync 批量关闭作品下的活跃会话（不加载到内存）
    private async Task CloseActiveSessionForWorkAsync(string workId, string userId, string status, string reason)
    {
        await db.AICreationSessions
            .Where(x => x.WorkId == workId && x.UserId == userId && x.Status == "active")
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.CloseReason, reason)
                .SetProperty(x => x.LastActivityAt, DateTime.Now));
    }

    // 反序列化已采纳内容 JSON，失败时返回空列表
    private static List<AdoptedItem> DeserializeAdopted(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<AdoptedItem>();
        return JsonHelper.Deserialize<List<AdoptedItem>>(json) ?? new List<AdoptedItem>();
    }

    // 将会话实体映射为响应 DTO
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
