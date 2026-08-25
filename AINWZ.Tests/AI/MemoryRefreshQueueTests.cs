using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Memory;

namespace AINWZ.Tests.AI;

public sealed class MemoryRefreshQueueTests
{
    [Fact]
    public async Task EnqueueAsync_DoesNotBlockOrThrowWhenQueueIsFullAndRequestIsCanceled()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var queue = new MemoryRefreshQueue(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MemoryRefreshQueue>.Instance);

        for (var i = 0; i < 512; i++)
            await queue.EnqueueAsync(new MemoryRefreshRequest { SessionId = $"session-{i}" });

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var enqueue = queue.EnqueueAsync(
            new MemoryRefreshRequest { SessionId = "overflow" },
            cancellation.Token);

        await enqueue;
        queue.Dispose();
    }

    [Fact]
    public async Task EnqueueAsync_CoalescesLatestRequestForSameSession()
    {
        var memory = new FakeMemoryProvider { BlockFirstRefresh = true };
        await using var services = new ServiceCollection()
            .AddSingleton<SpeakEase.Write.Application.Abstractions.AI.IMemoryProvider>(memory)
            .BuildServiceProvider();
        var queue = new MemoryRefreshQueue(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MemoryRefreshQueue>.Instance);

        await queue.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(new MemoryRefreshRequest
        {
            UserId = "user-1", WorkId = "work-1", SessionId = "session-1", TurnNumber = 1
        });
        await memory.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await queue.EnqueueAsync(new MemoryRefreshRequest
        {
            UserId = "user-1", WorkId = "work-1", SessionId = "session-1", TurnNumber = 2
        });
        await queue.EnqueueAsync(new MemoryRefreshRequest
        {
            UserId = "user-1", WorkId = "work-1", SessionId = "session-1", TurnNumber = 3
        });

        memory.ReleaseFirstRefresh();
        await SpinWaitAsync(() => memory.Refreshes.Count >= 2);
        await queue.StopAsync(CancellationToken.None);

        Assert.Equal(2, memory.Refreshes.Count);
        Assert.Equal(3, memory.Refreshes[^1].TurnNumber);
    }

    private static async Task SpinWaitAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 40 && !predicate(); i++)
            await Task.Delay(25);

        Assert.True(predicate());
    }
}
