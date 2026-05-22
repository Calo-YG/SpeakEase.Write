using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Persistence;
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

        var now = DateTime.Now;
        var summary = BuildSummary(messages, turnNumber);
        var snapshotJson = JsonHelper.Serialize(new
        {
            source = "session-memory-v1",
            userId,
            workId,
            sessionId,
            turnNumber,
            messageCount = messages.Count,
            generatedAt = now,
            summary
        });

        var entity = new MemorySnapshotEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = userId,
            WorkId = workId,
            SessionId = sessionId,
            SnapshotType = SnapshotType,
            Summary = summary,
            SnapshotJson = snapshotJson,
            VersionId = turnNumber.ToString(CultureInfo.InvariantCulture),
            CreateBy = userId,
            UpdateBy = userId,
            CreateAt = now,
            UpdateAt = now
        };

        db.MemorySnapshots.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        await cache.RefreshAsync(
            CacheKey(userId, workId, sessionId),
            ToSnapshot(entity),
            MemExpiry,
            RedisExpiry);

        logger.LogDebug(
            "Session memory refreshed: UserId={UserId}, WorkId={WorkId}, SessionId={SessionId}, Turn={Turn}",
            userId, workId, sessionId, turnNumber);
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
        var entity = await db.MemorySnapshots
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.WorkId == workId &&
                        x.SessionId == sessionId &&
                        x.SnapshotType == SnapshotType)
            .OrderByDescending(x => x.CreateAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

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
