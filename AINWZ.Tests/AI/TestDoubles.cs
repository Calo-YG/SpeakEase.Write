using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.AI;

internal static class TestDb
{
    public static SpeakEaseDbContext Create()
    {
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SpeakEaseDbContext(options);
    }
}

internal sealed class TestUserContext(string userId = "user-1") : IUserContext
{
    public string UserId { get; set; } = userId;
    public string UserName { get; set; } = "Test User";
    public string UserAccount { get; set; } = "test";
}

internal sealed class SequentialIdGenerator : ISnowflakeIdGenerator
{
    private long _next = 1000;

    public long NextId()
    {
        return Interlocked.Increment(ref _next);
    }

    public string NextIdString()
    {
        return NextId().ToString();
    }
}

internal sealed class FakeMultiCacheService : IMultiCacheService
{
    private readonly Dictionary<string, object> _items = new();

    public List<string> RemovedKeys { get; } = new();
    public List<string> RefreshedKeys { get; } = new();

    public async Task<TCache> GetOrSetAsync<TCache>(
        string key,
        Func<Task<TCache>> func,
        Action error = null,
        TimeSpan? memoryExpiry = null,
        TimeSpan? redisExpiry = null,
        int jitterSeconds = 30)
    {
        if (_items.TryGetValue(key, out var value))
            return (TCache)value;

        var created = await func();
        _items[key] = created;
        return created;
    }

    public Task RefreshAsync<TCache>(
        string key,
        TCache cache,
        TimeSpan? memoryExpiry = null,
        TimeSpan? redisExpiry = null,
        int jitterSeconds = 30)
    {
        _items[key] = cache;
        RefreshedKeys.Add(key);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _items.Remove(key);
        RemovedKeys.Add(key);
        return Task.CompletedTask;
    }
}

internal sealed class FakeMemoryProvider : IMemoryProvider
{
    public List<(string UserId, string WorkId, string SessionId, int TurnNumber)> Refreshes { get; } = new();
    public List<(string UserId, string WorkId, string SessionId)> Invalidations { get; } = new();
    public SessionMemorySnapshot Snapshot { get; set; } = SessionMemorySnapshot.Empty;
    public IReadOnlyList<MemoryFact> Facts { get; set; } = Array.Empty<MemoryFact>();
    public bool ThrowOnRefresh { get; set; }

    public Task<IReadOnlyList<MemoryFact>> LoadProjectFactsAsync(
        string userId,
        string workId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Facts);
    }

    public Task UpsertProjectFactAsync(
        string userId,
        string workId,
        MemoryFact fact,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<SessionMemorySnapshot> LoadSessionMemoryAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Snapshot);
    }

    public Task RefreshAfterTurnAsync(
        string userId,
        string workId,
        string sessionId,
        int turnNumber,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnRefresh)
            throw new InvalidOperationException("Simulated memory refresh failure.");

        Refreshes.Add((userId, workId, sessionId, turnNumber));
        return Task.CompletedTask;
    }

    public Task InvalidateSessionAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        Invalidations.Add((userId, workId, sessionId));
        return Task.CompletedTask;
    }

    public Task LoadAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SaveSnapshotAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string userId, string workId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal sealed class TestOpenAIContext : IOpenAIContext
{
    public string ApiKey { get; set; } = "test-key";
    public string Url { get; set; } = "https://example.test/v1/";
    public string Model { get; set; } = "test-model";
    public int MaxOutputTokens { get; set; } = 1024;
    public int ContextWindow { get; set; } = 32000;

    public Task ResolveAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string userId = null, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
