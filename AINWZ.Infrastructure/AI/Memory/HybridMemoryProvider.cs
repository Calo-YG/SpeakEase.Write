using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SpeakEase.Write.Application.Abstractions.AI;
using SessionMemorySnapshot = SpeakEase.Write.Application.Abstractions.AI.SessionMemorySnapshot;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Memory;

public sealed class HybridMemoryProvider(
    IMemoryDbContext db,
    IMultiCacheService cache,
    ISnowflakeIdGenerator idGenerator,
    ILogger<HybridMemoryProvider> logger) : IMemoryProvider, IMemoryRefreshFailureHandler
{
    private const string SnapshotType = "session-turn-summary";
    private const int MaxSnapshotMessages = 80;
    private const int MaxSummaryTurns = 12;
    private const int MaxContentChars = 420;
    private const int MaxSnapshotConcurrencyAttempts = 5;
    private static readonly TimeSpan MemExpiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RedisExpiry = TimeSpan.FromMinutes(10);

    public async Task<SessionMemorySnapshot> LoadSessionMemoryAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(workId) ||
            string.IsNullOrWhiteSpace(sessionId))
        {
            return SessionMemorySnapshot.Empty;
        }

        for (var attempt = 1; attempt <= MaxSnapshotConcurrencyAttempts; attempt++)
        {
            var memoryGeneration = await LoadMemoryGenerationAsync(
                userId, workId, sessionId, cancellationToken);
            var snapshot = await cache.GetOrSetAsync(
                CacheKey(userId, workId, sessionId, memoryGeneration),
                () => LoadLatestSnapshotAsync(
                    userId, workId, sessionId, memoryGeneration, cancellationToken),
                memoryExpiry: MemExpiry,
                redisExpiry: RedisExpiry) ?? SessionMemorySnapshot.Empty;
            var currentGeneration = await LoadMemoryGenerationAsync(
                userId, workId, sessionId, cancellationToken);
            if (currentGeneration == memoryGeneration)
                return snapshot;
        }

        throw new DbUpdateConcurrencyException(
            $"Session memory generation could not be read consistently: SessionId={sessionId}.");
    }

    public async Task<IReadOnlyList<SpeakEase.Write.Application.Abstractions.AI.MemoryFact>> LoadProjectFactsAsync(
        string userId,
        string workId,
        CancellationToken cancellationToken = default)
    {
        return await db.MemoryFacts
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.WorkId == workId &&
                        x.IsCurrent &&
                        (x.SessionId == string.Empty || db.AICreationSessions.Any(session =>
                            session.Id == x.SessionId &&
                            session.UserId == x.UserId &&
                            session.WorkId == x.WorkId &&
                            session.MemoryGeneration == x.MemoryGeneration)))
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Key)
            .Select(x => new SpeakEase.Write.Application.Abstractions.AI.MemoryFact
            {
                Id = x.Id,
                SessionId = x.SessionId,
                Category = x.Category,
                Key = x.Key,
                Value = x.Value,
                SourceTurn = x.SourceTurn,
                Confidence = x.Confidence,
                VersionTurn = x.VersionTurn
            })
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertProjectFactAsync(
        string userId,
        string workId,
        SpeakEase.Write.Application.Abstractions.AI.MemoryFact fact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(workId) ||
            string.IsNullOrWhiteSpace(fact.Category) || string.IsNullOrWhiteSpace(fact.Key))
            return;

        var memoryGeneration = string.IsNullOrWhiteSpace(fact.SessionId)
            ? 0
            : await LoadMemoryGenerationAsync(
                userId, workId, fact.SessionId, cancellationToken);
        await UpsertProjectFactAsync(
            userId, workId, fact, memoryGeneration, cancellationToken);
    }

    private async Task UpsertProjectFactAsync(
        string userId,
        string workId,
        SpeakEase.Write.Application.Abstractions.AI.MemoryFact fact,
        long memoryGeneration,
        CancellationToken cancellationToken)
    {
        var entity = await db.MemoryFacts.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.WorkId == workId && x.SessionId == (fact.SessionId ?? string.Empty) &&
            x.MemoryGeneration == memoryGeneration && x.Category == fact.Category && x.Key == fact.Key,
            cancellationToken);

        if (entity is not null &&
            (entity.VersionTurn > fact.VersionTurn ||
             entity.VersionTurn == fact.VersionTurn && entity.Confidence > fact.Confidence))
            return;

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new MemoryFactEntity
            {
                Id = idGenerator.NextIdString(),
                UserId = userId,
                WorkId = workId,
                SessionId = fact.SessionId ?? string.Empty,
                MemoryGeneration = memoryGeneration,
                Category = fact.Category,
                Key = fact.Key,
                CreateBy = userId,
                CreateAt = now
            };
            db.MemoryFacts.Add(entity);
        }

        entity.Value = fact.Value ?? string.Empty;
        entity.SourceTurn = fact.SourceTurn;
        entity.Confidence = fact.Confidence;
        entity.VersionTurn = fact.VersionTurn;
        entity.IsCurrent = true;
        entity.UpdateBy = userId;
        entity.UpdateAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync($"memory:project:{userId}:{workId}");
    }

    public async Task RefreshAfterTurnAsync(
        string userId,
        string workId,
        string sessionId,
        int turnNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(workId) ||
            string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var memoryGeneration = await LoadMemoryGenerationAsync(
            userId, workId, sessionId, cancellationToken);

        var messages = await db.AICreationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.Role != "tool")
            .OrderByDescending(m => m.TurnNumber)
            .ThenByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Role == "assistant" ? 1 : 0)
            .ThenByDescending(m => m.Id)
            .Take(MaxSnapshotMessages)
            .ToListAsync(cancellationToken);

        messages = messages
            .OrderBy(m => m.TurnNumber)
            .ThenBy(m => m.CreatedAt)
            .ThenBy(m => m.Role == "user" ? 0 : 1)
            .ThenBy(m => m.Id)
            .ToList();

        if (messages.Count == 0)
        {
            await db.MemorySnapshots
                .Where(x => x.UserId == userId &&
                            x.WorkId == workId &&
                            x.SessionId == sessionId &&
                            x.SnapshotType == SnapshotType &&
                            x.MemoryGeneration == memoryGeneration)
                .ExecuteDeleteAsync(cancellationToken);

            await cache.RemoveAsync(CacheKey(userId, workId, sessionId, memoryGeneration));
            return;
        }

        // 以数据库实际剩余消息的最大轮次为准，避免回滚后旧队列任务携带高版本号重建过期摘要。
        var effectiveTurnNumber = messages.Max(m => m.TurnNumber);

        var now = DateTime.UtcNow;
        var coveredToTurn = effectiveTurnNumber <= 4
            ? effectiveTurnNumber
            : effectiveTurnNumber - 4;
        var summaryMessages = messages
            .Where(x => x.TurnNumber <= coveredToTurn)
            .ToList();
        var coveredFromTurn = summaryMessages.Count == 0 ? 0 : summaryMessages.Min(x => x.TurnNumber);
        var summary = BuildSummary(summaryMessages, coveredToTurn);
        var snapshotJson = JsonHelper.Serialize(new
        {
            source = "session-memory-v1",
            userId,
            workId,
            sessionId,
            memoryGeneration,
            turnNumber = effectiveTurnNumber,
            coveredFromTurn,
            coveredToTurn,
            messageCount = messages.Count,
            generatedAt = now,
            summary
        });

        var entity = await db.MemorySnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.WorkId == workId &&
                        x.SessionId == sessionId &&
                        x.SnapshotType == SnapshotType &&
                        x.MemoryGeneration == memoryGeneration)
            .OrderByDescending(x => x.CreateAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var existingTurn = entity is null ||
                           !int.TryParse(entity.VersionId, out var parsedTurn)
            ? 0
            : parsedTurn;

        // 后完成的旧任务不能覆盖新版本；这也是缓存条件刷新的前置判断。
        if (entity is not null && existingTurn >= effectiveTurnNumber)
        {
            logger.LogDebug(
                "Skip stale session memory refresh: SessionId={SessionId}, ExistingTurn={ExistingTurn}, RequestedTurn={RequestedTurn}",
                sessionId,
                existingTurn,
                effectiveTurnNumber);
            await RefreshLatestSnapshotCacheAsync(
                userId, workId, sessionId, memoryGeneration, cancellationToken);
            return;
        }

        var versionId = effectiveTurnNumber.ToString(CultureInfo.InvariantCulture);
        if (entity is null)
        {
            entity = new MemorySnapshotEntity
            {
                Id = idGenerator.NextIdString(),
                UserId = userId,
                WorkId = workId,
                SessionId = sessionId,
                SnapshotType = SnapshotType,
                MemoryGeneration = memoryGeneration,
                CreateBy = userId,
                CreateAt = now
            };
            ApplySnapshot(entity, summary, snapshotJson, versionId, coveredFromTurn, coveredToTurn, userId, now);
            db.MemorySnapshots.Add(entity);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Another worker may have inserted the unique session row while this
                // refresh was building its summary. Detach the failed insert before
                // re-reading so the context cannot replay stale state on the next save.
                db.Detach(entity);
                entity = await LoadLatestSnapshotEntityAsync(
                    userId, workId, sessionId, memoryGeneration, cancellationToken);
                if (entity is null)
                    throw;

                if (GetSnapshotTurn(entity) >= effectiveTurnNumber)
                    return;

                if (!await TryUpdateSnapshotWithRetryAsync(
                        entity,
                        summary,
                        snapshotJson,
                        versionId,
                        coveredFromTurn,
                        coveredToTurn,
                        userId,
                        now,
                        cancellationToken))
                    return;
            }
        }
        else if (!await TryUpdateSnapshotWithRetryAsync(
                     entity,
                     summary,
                     snapshotJson,
                     versionId,
                     coveredFromTurn,
                     coveredToTurn,
                     userId,
                     now,
                     cancellationToken))
        {
            // A newer version won while this refresh was running.
            return;
        }

        await RefreshLatestSnapshotCacheAsync(
            userId, workId, sessionId, memoryGeneration, cancellationToken);

        foreach (var fact in ExtractFacts(messages, effectiveTurnNumber, sessionId))
            await UpsertProjectFactAsync(
                userId, workId, fact, memoryGeneration, cancellationToken);

        logger.LogDebug(
            "Session memory refreshed: UserId={UserId}, WorkId={WorkId}, SessionId={SessionId}, Turn={Turn}",
            userId, workId, sessionId, effectiveTurnNumber);
    }

    public async Task InvalidateSessionAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var memoryGeneration = await LoadMemoryGenerationAsync(
            userId, workId, sessionId, cancellationToken);
        await cache.RemoveAsync(CacheKey(userId, workId, sessionId, memoryGeneration));
        await cache.RemoveAsync(LegacySessionCacheKey(userId, workId, sessionId));
    }

    public async Task MarkStaleAsync(
        MemoryRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var memoryGeneration = await LoadMemoryGenerationAsync(
            request.UserId,
            request.WorkId,
            request.SessionId,
            cancellationToken);
        var entity = await db.MemorySnapshots.FirstOrDefaultAsync(x =>
            x.UserId == request.UserId &&
            x.WorkId == request.WorkId &&
            x.SessionId == request.SessionId &&
            x.SnapshotType == SnapshotType &&
            x.MemoryGeneration == memoryGeneration,
            cancellationToken);
        if (entity is not null && GetSnapshotTurn(entity) >= request.TurnNumber && entity.MemoryStatus == "fresh")
            return;

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new MemorySnapshotEntity
            {
                Id = idGenerator.NextIdString(),
                UserId = request.UserId,
                WorkId = request.WorkId,
                SessionId = request.SessionId,
                SnapshotType = SnapshotType,
                MemoryGeneration = memoryGeneration,
                VersionId = "0",
                CreateBy = request.UserId,
                CreateAt = now
            };
            db.MemorySnapshots.Add(entity);
        }

        entity.MemoryStatus = "stale";
        entity.UpdateBy = request.UserId;
        entity.UpdateAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKey(
            request.UserId,
            request.WorkId,
            request.SessionId,
            memoryGeneration));
    }

    public async Task PruneSessionFactsAfterTurnAsync(
        string userId,
        string workId,
        string sessionId,
        int targetTurn,
        CancellationToken cancellationToken = default)
    {
        var query = db.MemoryFacts.Where(x =>
            x.UserId == userId &&
            x.WorkId == workId &&
            x.SessionId == sessionId &&
            x.SourceTurn > targetTurn);

        try
        {
            await query.ExecuteDeleteAsync(cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ExecuteDelete", StringComparison.Ordinal))
        {
            var facts = await query.ToListAsync(cancellationToken);
            db.MemoryFacts.RemoveRange(facts);
            await db.SaveChangesAsync(cancellationToken);
        }

        await cache.RemoveAsync($"memory:project:{userId}:{workId}");
    }

    public Task LoadAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        return cache.GetOrSetAsync(
            LegacyCacheKey(userId, workId),
            () => Task.FromResult(string.Empty),
            memoryExpiry: MemExpiry,
            redisExpiry: RedisExpiry);
    }

    public Task SaveSnapshotAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        return cache.RefreshAsync(
            LegacyCacheKey(userId, workId),
            string.Empty,
            MemExpiry,
            RedisExpiry);
    }

    public async Task InvalidateAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(LegacyCacheKey(userId, workId));
    }

    private async Task<bool> TryUpdateSnapshotWithRetryAsync(
        MemorySnapshotEntity existing,
        string summary,
        string snapshotJson,
        string versionId,
        int coveredFromTurn,
        int coveredToTurn,
        string userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxSnapshotConcurrencyAttempts; attempt++)
        {
            var expectedVersionId = existing.VersionId;
            try
            {
                var affected = await db.MemorySnapshots
                    .Where(x => x.Id == existing.Id &&
                                x.UserId == existing.UserId &&
                                x.WorkId == existing.WorkId &&
                                x.SessionId == existing.SessionId &&
                                x.SnapshotType == existing.SnapshotType &&
                                x.MemoryGeneration == existing.MemoryGeneration &&
                                x.VersionId == expectedVersionId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Summary, summary)
                        .SetProperty(x => x.SnapshotJson, snapshotJson)
                        .SetProperty(x => x.VersionId, versionId)
                        .SetProperty(x => x.CoveredFromTurn, coveredFromTurn)
                        .SetProperty(x => x.CoveredToTurn, coveredToTurn)
                        .SetProperty(x => x.MemoryStatus, "fresh")
                        .SetProperty(x => x.UpdateBy, userId)
                        .SetProperty(x => x.UpdateAt, now), cancellationToken);

                if (affected == 1)
                {
                    ApplySnapshot(existing, summary, snapshotJson, versionId, coveredFromTurn, coveredToTurn, userId, now);
                    return true;
                }
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("ExecuteUpdate", StringComparison.OrdinalIgnoreCase))
            {
                // EF Core's in-memory provider does not translate ExecuteUpdate.
                var tracked = await db.MemorySnapshots
                    .FirstOrDefaultAsync(
                        x => x.Id == existing.Id &&
                             x.MemoryGeneration == existing.MemoryGeneration &&
                             x.VersionId == expectedVersionId,
                        cancellationToken);
                if (tracked is not null)
                {
                    ApplySnapshot(tracked, summary, snapshotJson, versionId, coveredFromTurn, coveredToTurn, userId, now);
                    await db.SaveChangesAsync(cancellationToken);
                    ApplySnapshot(existing, summary, snapshotJson, versionId, coveredFromTurn, coveredToTurn, userId, now);
                    return true;
                }
            }

            var current = await LoadLatestSnapshotEntityAsync(
                existing.UserId,
                existing.WorkId,
                existing.SessionId,
                existing.MemoryGeneration,
                cancellationToken);
            if (current is null)
                throw new DbUpdateConcurrencyException(
                    $"Session memory snapshot disappeared during update: SessionId={existing.SessionId}.");

            if (GetSnapshotTurn(current) >= int.Parse(versionId, CultureInfo.InvariantCulture))
                return false;

            existing = current;
        }

        throw new DbUpdateConcurrencyException(
            $"Session memory snapshot update exceeded {MaxSnapshotConcurrencyAttempts} attempts: " +
            $"SessionId={existing.SessionId}, DesiredVersion={versionId}.");
    }

    private async Task RefreshLatestSnapshotCacheAsync(
        string userId,
        string workId,
        string sessionId,
        long memoryGeneration,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxSnapshotConcurrencyAttempts; attempt++)
        {
            var latest = await LoadLatestSnapshotEntityAsync(
                userId, workId, sessionId, memoryGeneration, cancellationToken);
            if (latest is null)
            {
                await cache.RemoveAsync(CacheKey(userId, workId, sessionId, memoryGeneration));
                return;
            }

            await cache.RefreshAsync(
                CacheKey(userId, workId, sessionId, memoryGeneration),
                ToSnapshot(latest),
                MemExpiry,
                RedisExpiry);

            var current = await LoadLatestSnapshotEntityAsync(
                userId, workId, sessionId, memoryGeneration, cancellationToken);
            if (current is not null && current.Id == latest.Id && current.VersionId == latest.VersionId)
                return;
        }

        await cache.RemoveAsync(CacheKey(userId, workId, sessionId, memoryGeneration));
        throw new DbUpdateConcurrencyException(
            $"Session memory cache refresh could not observe a stable snapshot: SessionId={sessionId}.");
    }

    private async Task<MemorySnapshotEntity> LoadLatestSnapshotEntityAsync(
        string userId,
        string workId,
        string sessionId,
        long memoryGeneration,
        CancellationToken cancellationToken)
    {
        return await db.MemorySnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.WorkId == workId &&
                        x.SessionId == sessionId &&
                        x.SnapshotType == SnapshotType &&
                        x.MemoryGeneration == memoryGeneration)
            .OrderByDescending(x => x.CreateAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static int GetSnapshotTurn(MemorySnapshotEntity entity)
    {
        return int.TryParse(entity.VersionId, out var turn) ? turn : 0;
    }

    private static void ApplySnapshot(
        MemorySnapshotEntity entity,
        string summary,
        string snapshotJson,
        string versionId,
        int coveredFromTurn,
        int coveredToTurn,
        string userId,
        DateTime now)
    {
        entity.Summary = summary;
        entity.SnapshotJson = snapshotJson;
        entity.VersionId = versionId;
        entity.CoveredFromTurn = coveredFromTurn;
        entity.CoveredToTurn = coveredToTurn;
        entity.MemoryStatus = "fresh";
        entity.UpdateBy = userId;
        entity.UpdateAt = now;
    }

    private async Task<SessionMemorySnapshot> LoadLatestSnapshotAsync(
        string userId,
        string workId,
        string sessionId,
        long memoryGeneration,
        CancellationToken cancellationToken)
    {
        var entities = await db.MemorySnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.WorkId == workId &&
                        x.SessionId == sessionId &&
                        x.SnapshotType == SnapshotType &&
                        x.MemoryGeneration == memoryGeneration)
            .ToListAsync(cancellationToken);

        var entity = entities
            .OrderByDescending(x => int.TryParse(x.VersionId, out var turn) ? turn : 0)
            .ThenByDescending(x => x.CreateAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();

        return entity is null
            ? SessionMemorySnapshot.Empty
            : ToSnapshot(entity);
    }

    private static SessionMemorySnapshot ToSnapshot(MemorySnapshotEntity entity)
    {
        return new SessionMemorySnapshot
        {
            SnapshotId = entity.Id,
            Summary = entity.Summary,
            SnapshotJson = entity.SnapshotJson,
            TurnNumber = int.TryParse(entity.VersionId, out var turn) ? turn : 0,
            MemoryGeneration = entity.MemoryGeneration,
            CoveredFromTurn = entity.CoveredFromTurn,
            CoveredToTurn = entity.CoveredToTurn,
            MemoryStatus = entity.MemoryStatus,
            UpdatedAt = entity.CreateAt
        };
    }

    private static string BuildSummary(List<AICreationMessageEntity> messages, int turnNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Session summary through turn {turnNumber}.");

        foreach (var group in messages
            .GroupBy(m => m.TurnNumber)
            .OrderBy(g => g.Key)
            .TakeLast(MaxSummaryTurns))
        {
            sb.AppendLine($"Turn {group.Key}:");

            foreach (var message in group
                         .OrderBy(m => m.CreatedAt)
                         .ThenBy(m => m.Role == "user" ? 0 : 1)
                         .ThenBy(m => m.Id))
            {
                var role = message.Role == "assistant" ? "Assistant" : "User";
                sb.Append("- ");
                sb.Append(role);
                sb.Append(": ");
                sb.AppendLine(Truncate(message.Content, MaxContentChars));
            }
        }

        return sb.ToString().Trim();
    }

    private static IEnumerable<SpeakEase.Write.Application.Abstractions.AI.MemoryFact> ExtractFacts(
        IEnumerable<AICreationMessageEntity> messages,
        int versionTurn,
        string sessionId)
    {
        foreach (var message in messages)
        {
            foreach (var line in (message.Content ?? string.Empty).Split('\n'))
            {
                const string prefix = "[[fact:";
                if (!line.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                    !line.Contains("]]", StringComparison.Ordinal))
                    continue;

                var raw = line.Trim().Substring(prefix.Length, line.IndexOf("]]", StringComparison.Ordinal) - prefix.Length);
                var separator = raw.IndexOf(':');
                var equals = raw.IndexOf('=');
                if (separator <= 0 || equals <= separator)
                    continue;

                yield return new SpeakEase.Write.Application.Abstractions.AI.MemoryFact
                {
                    SessionId = sessionId,
                    Category = raw[..separator].Trim(),
                    Key = raw[(separator + 1)..equals].Trim(),
                    Value = raw[(equals + 1)..].Trim(),
                    SourceTurn = message.TurnNumber,
                    VersionTurn = versionTurn,
                    Confidence = 1
                };
            }
        }
    }

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return compact.Length <= maxChars ? compact : compact[..maxChars] + "...";
    }

    private async Task<long> LoadMemoryGenerationAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        return await db.AICreationSessions
            .AsNoTracking()
            .Where(x => x.Id == sessionId && x.UserId == userId && x.WorkId == workId)
            .Select(x => x.MemoryGeneration)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string CacheKey(
        string userId,
        string workId,
        string sessionId,
        long memoryGeneration)
    {
        return $"memory:session:{userId}:{workId}:{sessionId}:generation:{memoryGeneration}";
    }

    private static string LegacySessionCacheKey(string userId, string workId, string sessionId)
    {
        return $"memory:session:{userId}:{workId}:{sessionId}";
    }

    private static string LegacyCacheKey(string userId, string workId)
    {
        return $"memory:{userId}:{workId}";
    }
}
