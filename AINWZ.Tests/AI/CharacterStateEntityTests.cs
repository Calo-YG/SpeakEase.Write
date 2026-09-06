using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.AI.Character;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.AI;

public sealed class CharacterStateEntityTests
{
    [Fact]
    public async Task EnsureBaselineAsync_ProjectsLegacyCharacterFields()
    {
        await using var db = TestDb.Create();
        db.Characters.Add(new CharacterEntity
        {
            Id = "char-1",
            WorkId = "work-1",
            OwnerId = "user-1",
            Name = "林舟",
            Personality = "谨慎",
            Motivation = "保护家人",
            CreateBy = "user-1",
            UpdateBy = "user-1"
        });
        await db.SaveChangesAsync();
        var store = new CharacterStateStore(db, new TestUserContext(), new SequentialIdGenerator());

        var snapshot = await store.EnsureBaselineAsync("work-1", "char-1");

        Assert.Equal("char-1", snapshot.CharacterId);
        Assert.Equal(0, snapshot.Version);
        Assert.Contains("谨慎", snapshot.StateJson);
        Assert.Single(await db.CharacterStateSnapshots.ToListAsync());
    }

    [Fact]
    public async Task SaveSnapshotAsync_DoesNotOverwriteNewerVersion()
    {
        await using var db = TestDb.Create();
        var store = new CharacterStateStore(db, new TestUserContext(), new SequentialIdGenerator());

        await store.SaveSnapshotAsync(new CharacterStateSnapshotData
        {
            WorkId = "work-1",
            CharacterId = "char-1",
            StateJson = "new",
            Version = 2,
            Status = "confirmed"
        });
        await store.SaveSnapshotAsync(new CharacterStateSnapshotData
        {
            WorkId = "work-1",
            CharacterId = "char-1",
            StateJson = "old",
            Version = 1,
            Status = "confirmed"
        });

        var snapshot = await store.GetLatestSnapshotAsync("work-1", "char-1");

        Assert.Equal(2, snapshot.Version);
        Assert.Equal("new", snapshot.StateJson);
    }

    [Fact]
    public async Task TryCommitStateChangeAsync_CasFailureDoesNotLeaveOrphanEvent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseSqlite(connection)
            .Options;
        await SeedSnapshotAsync(options, version: 2);
        await using var db = new SpeakEaseDbContext(options);
        var store = new CharacterStateStore(db, new TestUserContext(), new SequentialIdGenerator());

        var committed = await store.TryCommitStateChangeAsync(
            Event("run-loser", "event-loser", version: 2),
            Snapshot(version: 2, state: "loser"),
            expectedVersion: 1);

        Assert.False(committed);
        Assert.Empty(await db.CharacterStateEvents.AsNoTracking().ToListAsync());
        Assert.Equal(2, (await db.CharacterStateSnapshots.AsNoTracking().SingleAsync()).Version);
    }

    [Fact]
    public async Task TryCommitStateChangeAsync_RebasesAfterConcurrentVersionAdvance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseSqlite(connection)
            .Options;
        await SeedSnapshotAsync(options, version: 1);
        await using var firstDb = new SpeakEaseDbContext(options);
        await using var secondDb = new SpeakEaseDbContext(options);
        var first = new CharacterStateStore(firstDb, new TestUserContext(), new SequentialIdGenerator());
        var second = new CharacterStateStore(secondDb, new TestUserContext(), new SequentialIdGenerator());

        Assert.True(await first.TryCommitStateChangeAsync(
            Event("run-first", "event-first", version: 2),
            Snapshot(version: 2, state: "first"),
            expectedVersion: 1));
        Assert.False(await second.TryCommitStateChangeAsync(
            Event("run-second", "event-second", version: 2),
            Snapshot(version: 2, state: "stale"),
            expectedVersion: 1));
        var current = await second.GetLatestSnapshotAsync("user-1", "work-1", "char-1");
        Assert.True(await second.TryCommitStateChangeAsync(
            Event("run-second", "event-second", version: current.Version + 1),
            Snapshot(version: current.Version + 1, state: "rebased"),
            expectedVersion: current.Version));

        await using var verification = new SpeakEaseDbContext(options);
        Assert.Equal(3, (await verification.CharacterStateSnapshots.AsNoTracking().SingleAsync()).Version);
        Assert.Equal(2, await verification.CharacterStateEvents.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task TryCommitStateChangeAsync_ReplaysSameSourceEventIdempotently()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseSqlite(connection)
            .Options;
        await SeedSnapshotAsync(options, version: 1);
        await using var db = new SpeakEaseDbContext(options);
        var store = new CharacterStateStore(db, new TestUserContext(), new SequentialIdGenerator());
        var stateEvent = Event("run-same", "event-same", version: 2);

        Assert.True(await store.TryCommitStateChangeAsync(
            stateEvent,
            Snapshot(version: 2, state: "committed"),
            expectedVersion: 1));
        Assert.True(await store.TryCommitStateChangeAsync(
            stateEvent,
            Snapshot(version: 3, state: "must-not-overwrite"),
            expectedVersion: 2));

        Assert.Single(await db.CharacterStateEvents.AsNoTracking().ToListAsync());
        var snapshot = await db.CharacterStateSnapshots.AsNoTracking().SingleAsync();
        Assert.Equal(2, snapshot.Version);
        Assert.Equal("committed", snapshot.StateJson);
    }

    private static async Task SeedSnapshotAsync(DbContextOptions<SpeakEaseDbContext> options, long version)
    {
        await using var db = new SpeakEaseDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.CharacterStateSnapshots.Add(new CharacterStateSnapshotEntity
        {
            Id = "snapshot-1",
            UserId = "user-1",
            WorkId = "work-1",
            CharacterId = "char-1",
            StateJson = "baseline",
            Version = version,
            Status = "confirmed",
            CreateBy = "user-1",
            UpdateBy = "user-1"
        });
        await db.SaveChangesAsync();
    }

    private static CharacterStateEventData Event(string runId, string eventKey, long version)
        => new()
        {
            UserId = "user-1",
            WorkId = "work-1",
            CharacterId = "char-1",
            SourceRunId = runId,
            SourceEventKey = eventKey,
            EventType = "state_change",
            Version = version,
            Confidence = 0.9
        };

    private static CharacterStateSnapshotData Snapshot(long version, string state)
        => new()
        {
            UserId = "user-1",
            WorkId = "work-1",
            CharacterId = "char-1",
            StateJson = state,
            Version = version,
            Status = "confirmed"
        };
}
