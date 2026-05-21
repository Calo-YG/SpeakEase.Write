using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Memory;

public sealed class HybridMemoryProvider(
    IServiceScopeFactory scopeFactory,
    IMultiCacheService cache,
    ILogger<HybridMemoryProvider> logger) : IMemoryProvider
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IMultiCacheService _cache = cache;
    private readonly ILogger<HybridMemoryProvider> _logger = logger;
    private static readonly TimeSpan MemExpiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RedisExpiry = TimeSpan.FromMinutes(10);

    public Task LoadAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"memory:{userId}:{workId}";

        return _cache.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                return string.Empty;
            },
            memoryExpiry: MemExpiry,
            redisExpiry: RedisExpiry);
    }

    public async Task SaveSnapshotAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"memory:{userId}:{workId}";
        try
        {
            await _cache.RefreshAsync(cacheKey, MemExpiry, RedisExpiry);
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
