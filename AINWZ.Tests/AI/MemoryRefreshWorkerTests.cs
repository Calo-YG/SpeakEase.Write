using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Memory;
using ApplicationMemoryProvider = SpeakEase.Write.Application.Abstractions.AI.IMemoryProvider;

namespace AINWZ.Tests.AI;

public sealed class MemoryRefreshWorkerTests
{
    [Fact]
    public async Task Worker_ContinuesAfterOneRequestExhaustsRetries()
    {
        var provider = new FailFirstRequestMemoryProvider();
        await using var services = new ServiceCollection()
            .AddSingleton<ApplicationMemoryProvider>(provider)
            .BuildServiceProvider();
        var worker = new MemoryRefreshQueue(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MemoryRefreshQueue>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.EnqueueAsync(Request("failed"));
        await WaitUntilAsync(() => provider.FailedAttempts >= 3);
        await worker.EnqueueAsync(Request("successful"));
        await WaitUntilAsync(() => provider.SuccessfulSessions.Contains("successful"));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, provider.FailedAttempts);
        Assert.Contains("successful", provider.SuccessfulSessions);
    }

    [Fact]
    public async Task Worker_MarksMemoryStaleAfterRetriesAreExhausted()
    {
        var provider = new FailFirstRequestMemoryProvider();
        await using var services = new ServiceCollection()
            .AddSingleton<ApplicationMemoryProvider>(provider)
            .AddSingleton<IMemoryRefreshFailureHandler>(provider)
            .BuildServiceProvider();
        var worker = new MemoryRefreshQueue(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MemoryRefreshQueue>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.EnqueueAsync(Request("failed"));
        await WaitUntilAsync(() => provider.StaleSessions.Contains("failed"));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, provider.FailedAttempts);
        Assert.Contains("failed", provider.StaleSessions);
    }

    private static MemoryRefreshRequest Request(string sessionId)
        => new()
        {
            UserId = "user-1",
            WorkId = "work-1",
            SessionId = sessionId,
            TurnNumber = 1
        };

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++)
            await Task.Delay(25);
        Assert.True(predicate());
    }

    private sealed class FailFirstRequestMemoryProvider : ApplicationMemoryProvider, IMemoryRefreshFailureHandler
    {
        public int FailedAttempts;
        public List<string> SuccessfulSessions { get; } = new();
        public List<string> StaleSessions { get; } = new();

        public Task RefreshAfterTurnAsync(string userId, string workId, string sessionId, int turnNumber, CancellationToken cancellationToken = default)
        {
            if (sessionId == "failed")
            {
                Interlocked.Increment(ref FailedAttempts);
                throw new InvalidOperationException("simulated refresh failure");
            }

            lock (SuccessfulSessions)
                SuccessfulSessions.Add(sessionId);
            return Task.CompletedTask;
        }

        public Task<SessionMemorySnapshot> LoadSessionMemoryAsync(string userId, string workId, string sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(SessionMemorySnapshot.Empty);
        public Task<IReadOnlyList<MemoryFact>> LoadProjectFactsAsync(string userId, string workId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemoryFact>>(Array.Empty<MemoryFact>());
        public Task UpsertProjectFactAsync(string userId, string workId, MemoryFact fact, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task InvalidateSessionAsync(string userId, string workId, string sessionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task PruneSessionFactsAfterTurnAsync(string userId, string workId, string sessionId, int targetTurn, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task LoadAsync(string userId, string workId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task SaveSnapshotAsync(string userId, string workId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task InvalidateAsync(string userId, string workId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkStaleAsync(MemoryRefreshRequest request, CancellationToken cancellationToken = default)
        {
            lock (StaleSessions)
                StaleSessions.Add(request.SessionId);
            return Task.CompletedTask;
        }
    }
}
