using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Infrastructure.AI.Memory;
using SpeakEase.Write.Infrastructure.MutilCache;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.AI;

public sealed class SessionMemoryProviderTests
{
    [Fact]
    public async Task LoadSessionMemoryAsync_LoadsOnlyMatchingSessionScope()
    {
        await using var db = TestDb.Create();
        var provider = CreateProvider(db);
        var now = DateTime.Now;

        db.MemorySnapshots.AddRange(
            new MemorySnapshotEntity
            {
                Id = "snap-current",
                UserId = "user-1",
                WorkId = "work-1",
                SessionId = "session-1",
                SnapshotType = "session-turn-summary",
                Summary = "current session",
                VersionId = "2",
                CreateAt = now
            },
            new MemorySnapshotEntity
            {
                Id = "snap-other-session",
                UserId = "user-1",
                WorkId = "work-1",
                SessionId = "session-2",
                SnapshotType = "session-turn-summary",
                Summary = "other session",
                VersionId = "3",
                CreateAt = now.AddMinutes(1)
            },
            new MemorySnapshotEntity
            {
                Id = "snap-other-user",
                UserId = "user-2",
                WorkId = "work-1",
                SessionId = "session-1",
                SnapshotType = "session-turn-summary",
                Summary = "other user",
                VersionId = "4",
                CreateAt = now.AddMinutes(2)
            });
        await db.SaveChangesAsync();

        var current = await provider.LoadSessionMemoryAsync("user-1", "work-1", "session-1");
        var wrongUser = await provider.LoadSessionMemoryAsync("user-2", "work-1", "session-2");

        Assert.Equal("snap-current", current.SnapshotId);
        Assert.Equal("current session", current.Summary);
        Assert.False(wrongUser.HasSnapshot);
    }

    [Fact]
    public async Task RefreshAfterTurnAsync_WritesSnapshotAndRefreshesCache()
    {
        await using var db = TestDb.Create();
        var cache = new FakeMultiCacheService();
        var provider = CreateProvider(db, cache);

        db.AICreationMessages.AddRange(
            new AICreationMessageEntity
            {
                Id = "msg-1",
                SessionId = "session-1",
                Role = "user",
                Content = "hello memory",
                TurnNumber = 1,
                CreatedAt = DateTime.Now
            },
            new AICreationMessageEntity
            {
                Id = "msg-2",
                SessionId = "session-1",
                Role = "assistant",
                Content = "answer memory",
                TurnNumber = 1,
                CreatedAt = DateTime.Now.AddSeconds(1)
            },
            new AICreationMessageEntity
            {
                Id = "msg-tool",
                SessionId = "session-1",
                Role = "tool",
                Content = "tool output should not be summarized",
                TurnNumber = 1,
                CreatedAt = DateTime.Now.AddSeconds(2)
            });
        await db.SaveChangesAsync();

        await provider.RefreshAfterTurnAsync("user-1", "work-1", "session-1", 1);
        var loaded = await provider.LoadSessionMemoryAsync("user-1", "work-1", "session-1");

        Assert.True(loaded.HasSnapshot);
        Assert.Equal(1, loaded.TurnNumber);
        Assert.Contains("hello memory", loaded.Summary);
        Assert.Contains("answer memory", loaded.Summary);
        Assert.DoesNotContain("tool output", loaded.Summary);
        Assert.Contains("memory:session:user-1:work-1:session-1", cache.RefreshedKeys);
    }

    [Fact]
    public async Task RefreshAfterTurnAsync_DoesNotLetOlderTurnOverwriteNewerSnapshot()
    {
        await using var db = TestDb.Create();
        var cache = new FakeMultiCacheService();
        var provider = CreateProvider(db, cache);

        db.AICreationMessages.AddRange(
            new AICreationMessageEntity
            {
                Id = "msg-1",
                SessionId = "session-1",
                Role = "user",
                Content = "turn one",
                TurnNumber = 1,
                CreatedAt = DateTime.Now
            },
            new AICreationMessageEntity
            {
                Id = "msg-2",
                SessionId = "session-1",
                Role = "assistant",
                Content = "turn one answer",
                TurnNumber = 1,
                CreatedAt = DateTime.Now.AddSeconds(1)
            },
            new AICreationMessageEntity
            {
                Id = "msg-3",
                SessionId = "session-1",
                Role = "user",
                Content = "turn two",
                TurnNumber = 2,
                CreatedAt = DateTime.Now.AddSeconds(2)
            },
            new AICreationMessageEntity
            {
                Id = "msg-4",
                SessionId = "session-1",
                Role = "assistant",
                Content = "turn two answer",
                TurnNumber = 2,
                CreatedAt = DateTime.Now.AddSeconds(3)
            });
        await db.SaveChangesAsync();

        await provider.RefreshAfterTurnAsync("user-1", "work-1", "session-1", 2);
        await provider.RefreshAfterTurnAsync("user-1", "work-1", "session-1", 1);

        var loaded = await provider.LoadSessionMemoryAsync("user-1", "work-1", "session-1");
        Assert.Equal(2, loaded.TurnNumber);
        Assert.Equal(1, db.MemorySnapshots.Count());
    }

    [Fact]
    public async Task RefreshAfterTurnAsync_DoesNotOverwriteNewerSnapshotWhenWritesRace()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"session-memory-{Guid.NewGuid():N}.db");
        try
        {
            var saveInterceptor = new PauseFirstSaveChangesInterceptor();
            await using var oldConnection = new SqliteConnection($"Data Source={databasePath}");
            await oldConnection.OpenAsync();
            var oldOptions = new DbContextOptionsBuilder<SpeakEaseDbContext>()
                .UseSqlite(oldConnection)
                .AddInterceptors(saveInterceptor)
                .Options;
            await using var oldDb = new SpeakEaseDbContext(oldOptions);
            await oldDb.Database.EnsureCreatedAsync();
            oldDb.AICreationMessages.AddRange(
                new AICreationMessageEntity
                {
                    Id = "race-msg-1",
                    SessionId = "race-session",
                    Role = "user",
                    Content = "turn one",
                    TurnNumber = 1,
                    CreatedAt = DateTime.Now
                },
                new AICreationMessageEntity
                {
                    Id = "race-msg-2",
                    SessionId = "race-session",
                    Role = "assistant",
                    Content = "turn one answer",
                    TurnNumber = 1,
                    CreatedAt = DateTime.Now.AddSeconds(1)
                });
            oldDb.MemorySnapshots.Add(new MemorySnapshotEntity
            {
                Id = "race-snapshot",
                UserId = "user-1",
                WorkId = "work-1",
                SessionId = "race-session",
                SnapshotType = "session-turn-summary",
                Summary = "initial",
                VersionId = "0",
                CreateAt = DateTime.Now
            });
            await oldDb.SaveChangesAsync();

            var cache = new FakeMultiCacheService();
            var oldProvider = CreateProvider(oldDb, cache);
            saveInterceptor.Arm();
            var oldRefresh = oldProvider.RefreshAfterTurnAsync(
                "user-1", "work-1", "race-session", 1);
            await saveInterceptor.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await using var newConnection = new SqliteConnection($"Data Source={databasePath}");
            await newConnection.OpenAsync();
            var newOptions = new DbContextOptionsBuilder<SpeakEaseDbContext>()
                .UseSqlite(newConnection)
                .Options;
            await using var newDb = new SpeakEaseDbContext(newOptions);
            newDb.AICreationMessages.AddRange(
                new AICreationMessageEntity
                {
                    Id = "race-msg-3",
                    SessionId = "race-session",
                    Role = "user",
                    Content = "turn two",
                    TurnNumber = 2,
                    CreatedAt = DateTime.Now.AddSeconds(2)
                },
                new AICreationMessageEntity
                {
                    Id = "race-msg-4",
                    SessionId = "race-session",
                    Role = "assistant",
                    Content = "turn two answer",
                    TurnNumber = 2,
                    CreatedAt = DateTime.Now.AddSeconds(3)
                });
            await newDb.SaveChangesAsync();

            var newProvider = CreateProvider(newDb, cache);
            try
            {
                await newProvider.RefreshAfterTurnAsync("user-1", "work-1", "race-session", 2);
            }
            finally
            {
                saveInterceptor.Release();
            }

            await oldRefresh;

            await using var verificationConnection = new SqliteConnection($"Data Source={databasePath}");
            await verificationConnection.OpenAsync();
            var verificationOptions = new DbContextOptionsBuilder<SpeakEaseDbContext>()
                .UseSqlite(verificationConnection)
                .Options;
            await using var verificationDb = new SpeakEaseDbContext(verificationOptions);
            var snapshot = await verificationDb.MemorySnapshots
                .AsNoTracking()
                .SingleAsync(x => x.SessionId == "race-session");
            Assert.Equal("2", snapshot.VersionId);
            Assert.Equal(2, (await newProvider.LoadSessionMemoryAsync(
                "user-1", "work-1", "race-session")).TurnNumber);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                try
                {
                    File.Delete(databasePath);
                }
                catch (IOException)
                {
                    // SQLite may release the native handle after the async disposals complete.
                }
            }
        }
    }

    [Fact]
    public async Task RefreshAfterTurnAsync_RetriesCasWhenOlderWriterWinsFirst()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"session-memory-cas-{Guid.NewGuid():N}.db");
        var oldInterceptor = new PauseMatchingCommandInterceptor("UPDATE \"memory_snapshots\"");
        var newInterceptor = new PauseMatchingCommandInterceptor("UPDATE \"memory_snapshots\"");
        try
        {
            await using var setupDb = await CreateSqliteDbAsync(databasePath);
            await SeedSnapshotRaceAsync(setupDb, "cas-session");

            await using var oldDb = await CreateSqliteDbAsync(databasePath, oldInterceptor);
            var oldProvider = CreateProvider(oldDb);
            var oldRefresh = oldProvider.RefreshAfterTurnAsync("user-1", "work-1", "cas-session", 1);
            await oldInterceptor.CommandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await using var messageDb = await CreateSqliteDbAsync(databasePath);
            AddTurnTwoMessages(messageDb, "cas-session", "cas");
            await messageDb.SaveChangesAsync();

            await using var newDb = await CreateSqliteDbAsync(databasePath, newInterceptor);
            var newProvider = CreateProvider(newDb);
            var newRefresh = newProvider.RefreshAfterTurnAsync("user-1", "work-1", "cas-session", 2);
            await newInterceptor.CommandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            oldInterceptor.Release();
            await oldRefresh;
            newInterceptor.Release();
            await newRefresh;

            await using var verificationDb = await CreateSqliteDbAsync(databasePath);
            var snapshot = await verificationDb.MemorySnapshots.AsNoTracking().SingleAsync();
            Assert.Equal("2", snapshot.VersionId);
        }
        finally
        {
            oldInterceptor.Release();
            newInterceptor.Release();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task RefreshAfterTurnAsync_RefreshesCacheFromLatestDatabaseSnapshot()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"session-memory-cache-{Guid.NewGuid():N}.db");
        var cache = new PauseFirstRefreshCache();
        try
        {
            await using var setupDb = await CreateSqliteDbAsync(databasePath);
            await SeedSnapshotRaceAsync(setupDb, "cache-session");

            await using var oldDb = await CreateSqliteDbAsync(databasePath);
            var oldProvider = CreateProvider(oldDb, cache);
            var oldRefresh = oldProvider.RefreshAfterTurnAsync("user-1", "work-1", "cache-session", 1);
            await cache.FirstRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await using var newDb = await CreateSqliteDbAsync(databasePath);
            AddTurnTwoMessages(newDb, "cache-session", "cache");
            await newDb.SaveChangesAsync();
            var newProvider = CreateProvider(newDb, cache);
            await newProvider.RefreshAfterTurnAsync("user-1", "work-1", "cache-session", 2);

            cache.Release();
            await oldRefresh;

            Assert.Equal(2, cache.Snapshot.TurnNumber);
        }
        finally
        {
            cache.Release();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task RefreshAfterTurnAsync_RepairsStaleCacheWhenDatabaseVersionIsAlreadyNewer()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"session-memory-stale-cache-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = await CreateSqliteDbAsync(databasePath);
            await SeedSnapshotRaceAsync(db, "stale-cache-session");
            await db.MemorySnapshots.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.VersionId, "2")
                .SetProperty(x => x.Summary, "database version two"));
            var cache = new MutableSnapshotCache
            {
                Snapshot = new SpeakEase.Write.Application.Abstractions.AI.SessionMemorySnapshot
                {
                    SnapshotId = "snapshot-stale-cache-session",
                    TurnNumber = 1,
                    Summary = "stale cache"
                }
            };
            var provider = CreateProvider(db, cache);

            await provider.RefreshAfterTurnAsync("user-1", "work-1", "stale-cache-session", 1);
            var loaded = await provider.LoadSessionMemoryAsync(
                "user-1", "work-1", "stale-cache-session");

            Assert.Equal(2, loaded.TurnNumber);
            Assert.Equal("database version two", loaded.Summary);
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task RefreshAfterTurnAsync_InvalidatesCacheWhenVersionNeverStabilizes()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"session-memory-unstable-cache-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = await CreateSqliteDbAsync(databasePath);
            await SeedSnapshotRaceAsync(db, "unstable-cache-session");
            var cache = new MutableSnapshotCache();
            cache.AfterRefreshAsync = async () =>
            {
                await using var mutationDb = await CreateSqliteDbAsync(databasePath);
                var currentVersion = await mutationDb.MemorySnapshots
                    .AsNoTracking()
                    .Select(x => x.VersionId)
                    .SingleAsync();
                var nextVersion = (int.Parse(currentVersion) + 1).ToString();
                await mutationDb.MemorySnapshots.ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.VersionId, nextVersion));
            };
            var provider = CreateProvider(db, cache);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                provider.RefreshAfterTurnAsync("user-1", "work-1", "unstable-cache-session", 1));

            Assert.True(cache.WasRemoved);
            Assert.Null(cache.Snapshot);
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task RefreshAfterTurnAsync_ConcurrentInitialInsertKeepsSingleLatestSnapshot()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"session-memory-insert-{Guid.NewGuid():N}.db");
        var oldInterceptor = new PauseMatchingCommandInterceptor("INSERT INTO \"memory_snapshots\"");
        try
        {
            await using var setupDb = await CreateSqliteDbAsync(databasePath);
            await SeedMessagesAsync(setupDb, "insert-session");

            await using var oldDb = await CreateSqliteDbAsync(databasePath, oldInterceptor);
            var oldProvider = CreateProvider(oldDb);
            var oldRefresh = oldProvider.RefreshAfterTurnAsync("user-1", "work-1", "insert-session", 1);
            await oldInterceptor.CommandStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await using var newDb = await CreateSqliteDbAsync(databasePath);
            AddTurnTwoMessages(newDb, "insert-session", "insert");
            await newDb.SaveChangesAsync();
            var newProvider = CreateProvider(newDb);
            await newProvider.RefreshAfterTurnAsync("user-1", "work-1", "insert-session", 2);

            oldInterceptor.Release();
            await oldRefresh;

            await using var verificationDb = await CreateSqliteDbAsync(databasePath);
            var snapshots = await verificationDb.MemorySnapshots.AsNoTracking().ToListAsync();
            Assert.Single(snapshots);
            Assert.Equal("2", snapshots[0].VersionId);
        }
        finally
        {
            oldInterceptor.Release();
            TryDelete(databasePath);
        }
    }

    [Fact]
    public async Task PruneSessionFactsAfterTurnAsync_RemovesOnlyFactsFromRolledBackTurns()
    {
        await using var db = TestDb.Create();
        var cache = new FakeMultiCacheService();
        var provider = CreateProvider(db, cache);
        db.MemoryFacts.AddRange(
            new MemoryFactEntity
            {
                Id = "fact-1", UserId = "user-1", WorkId = "work-1", SessionId = "session-1",
                Category = "character", Key = "name", Value = "valid", SourceTurn = 1, VersionTurn = 2
            },
            new MemoryFactEntity
            {
                Id = "fact-2", UserId = "user-1", WorkId = "work-1", SessionId = "session-1",
                Category = "plot", Key = "twist", Value = "stale", SourceTurn = 2, VersionTurn = 2
            },
            new MemoryFactEntity
            {
                Id = "fact-other", UserId = "user-1", WorkId = "work-1", SessionId = "session-2",
                Category = "plot", Key = "twist", Value = "other", SourceTurn = 3, VersionTurn = 3
            });
        await db.SaveChangesAsync();

        await provider.PruneSessionFactsAfterTurnAsync("user-1", "work-1", "session-1", 1);

        var remaining = await db.MemoryFacts.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(new[] { "fact-1", "fact-other" }, remaining.Select(x => x.Id));
        Assert.Contains("memory:project:user-1:work-1", cache.RemovedKeys);
    }

    private static HybridMemoryProvider CreateProvider(
        SpeakEase.Write.Infrastructure.Persistence.SpeakEaseDbContext db,
        IMultiCacheService cache = null)
    {
        return new HybridMemoryProvider(
            db,
            cache ?? new FakeMultiCacheService(),
            new SequentialIdGenerator(),
            NullLogger<HybridMemoryProvider>.Instance);
    }

    private static async Task<SpeakEaseDbContext> CreateSqliteDbAsync(
        string databasePath,
        IInterceptor interceptor = null)
    {
        var connection = new SqliteConnection($"Data Source={databasePath};Default Timeout=5");
        await connection.OpenAsync();
        var optionsBuilder = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseSqlite(connection);
        if (interceptor is not null)
            optionsBuilder.AddInterceptors(interceptor);

        var db = new SpeakEaseDbContext(optionsBuilder.Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task SeedSnapshotRaceAsync(SpeakEaseDbContext db, string sessionId)
    {
        await SeedMessagesAsync(db, sessionId);
        db.MemorySnapshots.Add(new MemorySnapshotEntity
        {
            Id = $"snapshot-{sessionId}",
            UserId = "user-1",
            WorkId = "work-1",
            SessionId = sessionId,
            SnapshotType = "session-turn-summary",
            Summary = "initial",
            VersionId = "0",
            CreateAt = DateTime.Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedMessagesAsync(SpeakEaseDbContext db, string sessionId)
    {
        db.AICreationMessages.AddRange(
            new AICreationMessageEntity
            {
                Id = $"{sessionId}-msg-1",
                SessionId = sessionId,
                Role = "user",
                Content = "turn one",
                TurnNumber = 1,
                CreatedAt = DateTime.Now
            },
            new AICreationMessageEntity
            {
                Id = $"{sessionId}-msg-2",
                SessionId = sessionId,
                Role = "assistant",
                Content = "turn one answer",
                TurnNumber = 1,
                CreatedAt = DateTime.Now.AddSeconds(1)
            });
        await db.SaveChangesAsync();
    }

    private static void AddTurnTwoMessages(SpeakEaseDbContext db, string sessionId, string idPrefix)
    {
        db.AICreationMessages.AddRange(
            new AICreationMessageEntity
            {
                Id = $"{idPrefix}-msg-3",
                SessionId = sessionId,
                Role = "user",
                Content = "turn two",
                TurnNumber = 2,
                CreatedAt = DateTime.Now.AddSeconds(2)
            },
            new AICreationMessageEntity
            {
                Id = $"{idPrefix}-msg-4",
                SessionId = sessionId,
                Role = "assistant",
                Content = "turn two answer",
                TurnNumber = 2,
                CreatedAt = DateTime.Now.AddSeconds(3)
            });
    }

    private static void TryDelete(string databasePath)
    {
        if (!File.Exists(databasePath))
            return;

        try
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
        catch (IOException)
        {
            // The OS will clean temporary test files if SQLite releases late.
        }
    }

    private sealed class PauseMatchingCommandInterceptor(string commandPrefix) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _paused;

        public TaskCompletionSource CommandStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await PauseIfMatchingAsync(command);

            return result;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await PauseIfMatchingAsync(command);
            return result;
        }

        private async Task PauseIfMatchingAsync(DbCommand command)
        {
            if (!command.CommandText.TrimStart().StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase) ||
                Interlocked.CompareExchange(ref _paused, 1, 0) != 0)
                return;

            CommandStarted.TrySetResult();
            await _release.Task;
        }
    }

    private sealed class PauseFirstRefreshCache : IMultiCacheService
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _refreshCount;

        public TaskCompletionSource FirstRefreshStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SpeakEase.Write.Application.Abstractions.AI.SessionMemorySnapshot Snapshot { get; private set; }

        public void Release() => _release.TrySetResult();

        public async Task<TCache> GetOrSetAsync<TCache>(
            string key,
            Func<Task<TCache>> func,
            Action error = null,
            TimeSpan? memoryExpiry = null,
            TimeSpan? redisExpiry = null,
            int jitterSeconds = 30)
        {
            if (Snapshot is TCache snapshot)
                return snapshot;
            return await func();
        }

        public async Task RefreshAsync<TCache>(
            string key,
            TCache cache,
            TimeSpan? memoryExpiry = null,
            TimeSpan? redisExpiry = null,
            int jitterSeconds = 30)
        {
            if (Interlocked.Increment(ref _refreshCount) == 1)
            {
                FirstRefreshStarted.TrySetResult();
                await _release.Task;
            }

            Snapshot = cache as SpeakEase.Write.Application.Abstractions.AI.SessionMemorySnapshot;
        }

        public Task RemoveAsync(string key)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }
    }

    private sealed class MutableSnapshotCache : IMultiCacheService
    {
        public SpeakEase.Write.Application.Abstractions.AI.SessionMemorySnapshot Snapshot { get; set; }
        public Func<Task> AfterRefreshAsync { get; set; }
        public bool WasRemoved { get; private set; }

        public async Task<TCache> GetOrSetAsync<TCache>(
            string key,
            Func<Task<TCache>> func,
            Action error = null,
            TimeSpan? memoryExpiry = null,
            TimeSpan? redisExpiry = null,
            int jitterSeconds = 30)
        {
            if (Snapshot is TCache snapshot)
                return snapshot;

            return await func();
        }

        public async Task RefreshAsync<TCache>(
            string key,
            TCache cache,
            TimeSpan? memoryExpiry = null,
            TimeSpan? redisExpiry = null,
            int jitterSeconds = 30)
        {
            Snapshot = cache as SpeakEase.Write.Application.Abstractions.AI.SessionMemorySnapshot;
            if (AfterRefreshAsync is not null)
                await AfterRefreshAsync();
        }

        public Task RemoveAsync(string key)
        {
            Snapshot = null;
            WasRemoved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class PauseFirstSaveChangesInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;

        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm() => Interlocked.Exchange(ref _armed, 1);

        public void Release() => _release.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) == 1 && SaveStarted.TrySetResult())
                await _release.Task;

            return result;
        }
    }
}
