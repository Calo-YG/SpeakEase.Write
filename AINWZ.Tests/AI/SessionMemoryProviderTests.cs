using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Infrastructure.AI.Memory;

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
}
