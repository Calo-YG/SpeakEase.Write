using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Memory;

public sealed class HybridMemoryProvider : IMemoryProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMultiCacheService _cache;
    private readonly ILogger<HybridMemoryProvider> _logger;
    private static readonly TimeSpan MemExpiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RedisExpiry = TimeSpan.FromMinutes(10);

    public HybridMemoryProvider(
        IServiceScopeFactory scopeFactory,
        IMultiCacheService cache,
        ILogger<HybridMemoryProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public Task<MemoryContext> LoadAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"memory:{userId}:{workId}";

        return _cache.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

                var work = await db.Works.AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == workId && w.UserId == userId, cancellationToken);

                if (work is null)
                    return new MemoryContext();

                var chapters = await db.Chapters.AsNoTracking()
                    .Where(c => c.WorkId == workId)
                    .OrderByDescending(c => c.Sequence)
                    .Take(10)
                    .Select(c => new MemoryChapter
                    {
                        Title = c.Title,
                        Sequence = c.Sequence,
                        Summary = c.Summary,
                        WordCount = c.WordCount,
                        Status = c.Status
                    })
                    .ToListAsync(cancellationToken);

                var characters = await db.Characters.AsNoTracking()
                    .Where(c => c.WorkId == workId)
                    .Take(30)
                    .Select(c => new MemoryCharacter
                    {
                        Name = c.Name,
                        Identity = c.Identity,
                        Personality = c.Personality,
                        RoleSummary = c.BackgroundStory
                    })
                    .ToListAsync(cancellationToken);

                var outlines = await db.OutlineNodes.AsNoTracking()
                    .Where(o => o.WorkId == workId)
                    .OrderBy(o => o.Sequence)
                    .Take(50)
                    .Select(o => new MemoryOutlineNode
                    {
                        Title = o.Title,
                        Description = o.Goal,
                        Sequence = o.Sequence,
                        ChapterId = string.Empty
                    })
                    .ToListAsync(cancellationToken);

                var foreshadowings = await db.Foreshadowings.AsNoTracking()
                    .Where(f => f.WorkId == workId && f.Status != "resolved")
                    .Take(30)
                    .Select(f => new MemoryForeshadowing
                    {
                        Title = f.Title,
                        Status = f.Status
                    })
                    .ToListAsync(cancellationToken);

                var worldSetting = await db.WorldSettings.AsNoTracking()
                    .Where(w => w.WorkId == workId)
                    .Select(w => w.Summary)
                    .FirstOrDefaultAsync(cancellationToken);

                var timelineEvents = await db.TimelineEvents.AsNoTracking()
                    .Where(t => t.WorkId == workId)
                    .OrderBy(t => t.EventTime)
                    .Take(20)
                    .Select(t => new MemoryTimelineEvent
                    {
                        Title = t.Title,
                        Description = t.Description,
                        EventTime = t.EventTime,
                        EventType = t.EventType,
                        ChapterId = t.ChapterId
                    })
                    .ToListAsync(cancellationToken);

                return new MemoryContext
                {
                    WorkTitle = work.Title,
                    Genre = work.Genre,
                    Perspective = work.Perspective,
                    TotalWordCount = work.TotalWordCount,
                    WorkSummary = work.Summary,
                    RecentChapters = chapters.OrderBy(c => c.Sequence).ToList(),
                    Characters = characters,
                    OutlineNodes = outlines,
                    WorldSettingSummary = worldSetting ?? string.Empty,
                    ActiveForeshadowings = foreshadowings,
                    TimelineEvents = timelineEvents
                };
            },
            memoryExpiry: MemExpiry,
            redisExpiry: RedisExpiry);
    }

    public async Task SaveSnapshotAsync(string userId, string workId, MemoryContext ctx, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"memory:{userId}:{workId}";
        try
        {
            await _cache.RefreshAsync(cacheKey, ctx, MemExpiry, RedisExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory SaveSnapshot 失败: UserId={UserId}, WorkId={WorkId}", userId, workId);
        }
    }

    public async Task InvalidateAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"memory:{userId}:{workId}";
        try
        {
            await _cache.RemoveAsync(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory Invalidate 失败: UserId={UserId}, WorkId={WorkId}", userId, workId);
        }
    }
}
