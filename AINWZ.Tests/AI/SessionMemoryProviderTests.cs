using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Infrastructure.AI.Memory;
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
            await newProvider.RefreshAfterTurnAsync("user-1", "work-1", "race-session", 2);
            saveInterceptor.Release();

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
        FakeMultiCacheService cache = null)
    {
        return new HybridMemoryProvider(
            db,
            cache ?? new FakeMultiCacheService(),
            new SequentialIdGenerator(),
            NullLogger<HybridMemoryProvider>.Instance);
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
