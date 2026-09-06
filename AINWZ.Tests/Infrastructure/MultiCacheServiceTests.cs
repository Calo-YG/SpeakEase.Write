using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.Write.Infrastructure.MutilCache;

namespace AINWZ.Tests.Infrastructure;

public sealed class MultiCacheServiceTests
{
    [Fact]
    public async Task RemoveAsync_WaitsForInFlightRefillBeforeInvalidating()
    {
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var distributed = new MemoryDistributedCache(new Microsoft.Extensions.Options.OptionsWrapper<MemoryDistributedCacheOptions>(new()));
        var service = new MultiCacheService(memory, distributed, NullLogger<MultiCacheService>.Instance);
        var refillStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefill = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var refill = service.GetOrSetAsync(
            "cache-key",
            async () =>
            {
                refillStarted.SetResult();
                await releaseRefill.Task;
                return "stale";
            });

        await refillStarted.Task;
        var remove = service.RemoveAsync("cache-key");
        await Task.Delay(20);
        Assert.False(remove.IsCompleted);

        releaseRefill.SetResult();
        await Task.WhenAll(refill, remove);

        var result = await service.GetOrSetAsync("cache-key", () => Task.FromResult("fresh"));

        Assert.Equal("fresh", result);
    }
}
