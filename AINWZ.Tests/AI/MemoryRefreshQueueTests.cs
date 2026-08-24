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
}
