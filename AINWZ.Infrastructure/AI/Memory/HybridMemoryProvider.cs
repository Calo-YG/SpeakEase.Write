using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Persistence;
using SessionMemorySnapshot = SpeakEase.Write.Application.Abstractions.AI.SessionMemorySnapshot;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Memory;

public sealed class HybridMemoryProvider(
    SpeakEaseDbContext db,
    IMultiCacheService cache,
    ISnowflakeIdGenerator idGenerator,
    ILogger<HybridMemoryProvider> logger) : IMemoryProvider
{
    private const string SnapshotType = "session-turn-summary";
    private const int MaxSnapshotMessages = 80;
    private const int MaxSummaryTurns = 12;
    private const int MaxContentChars = 420;
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

        return await cache.GetOrSetAsync(
            CacheKey(userId, workId, sessionId),
            () => LoadLatestSnapshotAsync(userId, workId, sessionId, cancellationToken),
            memoryExpiry: MemExpiry,
            redisExpiry: RedisExpiry) ?? SessionMemorySnapshot.Empty;
    }

    public async Task<IReadOnlyList<SpeakEase.Write.Application.Abstractions.AI.MemoryFact>> LoadProjectFactsAsync(
        string userId,
        string workId,
        CancellationToken cancellationToken = default)
    {
        return await db.MemoryFacts
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.WorkId == workId && x.IsCurrent)
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

        var entity = await db.MemoryFacts.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.WorkId == workId && x.SessionId == (fact.SessionId ?? string.Empty) &&
            x.Category == fact.Category && x.Key == fact.Key, cancellationToken);

        if (entity is not null &&
            (entity.VersionTurn > fact.VersionTurn ||
             entity.VersionTurn == fact.VersionTurn && entity.Confidence > fact.Confidence))
            return;

        var now = DateTime.Now;
        entity ??= new MemoryFactEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = userId,
            WorkId = workId,
            SessionId = fact.SessionId ?? string.Empty,
            Category = fact.Category,
            Key = fact.Key,
            CreateBy = userId,
            CreateAt = now
        };
        if (entity.Id is not null && db.Entry(entity).State == EntityState.Detached)
            db.MemoryFacts.Add(entity);

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

        var messages = await db.AICreationMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.Role != "tool")
            .OrderByDescending(m => m.TurnNumber)
            .ThenByDescending(m => m.CreatedAt)
            .Take(MaxSnapshotMessages)
            .ToListAsync(cancellationToken);

        messages = messages
            .OrderBy(m => m.TurnNumber)
            .ThenBy(m => m.CreatedAt)
            .ToList();

        if (messages.Count == 0)
        {
            await db.MemorySnapshots
                .Where(x => x.UserId == userId &&
                            x.WorkId == workId &&
                            x.SessionId == sessionId &&
                            x.SnapshotType == SnapshotType)
                .ExecuteDeleteAsync(cancellationToken);

            await InvalidateSessionAsync(userId, workId, sessionId, cancellationToken);
            return;
        }

        // 以数据库实际剩余消息的最大轮次为准，避免回滚后旧队列任务携带高版本号重建过期摘要。
        var effectiveTurnNumber = messages.Max(m => m.TurnNumber);

        var now = DateTime.Now;
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
            turnNumber = effectiveTurnNumber,
            coveredFromTurn,
            coveredToTurn,
            messageCount = messages.Count,
            generatedAt = now,
            summary
        });

        var entity = await db.MemorySnapshots
            .Where(x => x.UserId == userId &&
                        x.WorkId == workId &&
                        x.SessionId == sessionId &&
                        x.SnapshotType == SnapshotType)
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
            return;
        }

        if (entity is null)
        {
            entity = new MemorySnapshotEntity
            {
                Id = idGenerator.NextIdString(),
                UserId = userId,
                WorkId = workId,
                SessionId = sessionId,
                SnapshotType = SnapshotType,
                CreateBy = userId,
                CreateAt = now
            };
            db.MemorySnapshots.Add(entity);
        }

        entity.Summary = summary;
        entity.SnapshotJson = snapshotJson;
        entity.VersionId = effectiveTurnNumber.ToString(CultureInfo.InvariantCulture);
        entity.CoveredFromTurn = coveredFromTurn;
        entity.CoveredToTurn = coveredToTurn;
        entity.MemoryStatus = "fresh";
        entity.UpdateBy = userId;
        entity.UpdateAt = now;
        await db.SaveChangesAsync(cancellationToken);

        await cache.RefreshAsync(
            CacheKey(userId, workId, sessionId),
            ToSnapshot(entity),
            MemExpiry,
            RedisExpiry);

        foreach (var fact in ExtractFacts(messages, effectiveTurnNumber, sessionId))
            await UpsertProjectFactAsync(userId, workId, fact, cancellationToken);

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
        await cache.RemoveAsync(CacheKey(userId, workId, sessionId));
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

    private async Task<SessionMemorySnapshot> LoadLatestSnapshotAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var entities = await db.MemorySnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.WorkId == workId &&
                        x.SessionId == sessionId &&
                        x.SnapshotType == SnapshotType)
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

            foreach (var message in group.OrderBy(m => m.CreatedAt))
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

    private static string CacheKey(string userId, string workId, string sessionId)
    {
        return $"memory:session:{userId}:{workId}:{sessionId}";
    }

    private static string LegacyCacheKey(string userId, string workId)
    {
        return $"memory:{userId}:{workId}";
    }
}
