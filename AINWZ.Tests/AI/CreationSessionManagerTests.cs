using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Application.Applications;
using SpeakEase.Write.Domain.Entities.AI;

namespace AINWZ.Tests.AI;

public sealed class CreationSessionManagerTests
{
    [Fact]
    public async Task AppendTurnAsync_IncrementsTurnAndSavesMessagesTogether()
    {
        await using var db = TestDb.Create();
        var memory = new FakeMemoryProvider();
        var manager = CreateManager(db, memory);

        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-1",
            UserId = "user-1",
            WorkId = "work-1",
            Status = "active",
            TurnCount = 0,
            AdoptedContentJson = "[]"
        });
        await db.SaveChangesAsync();

        var result = await manager.AppendTurnAsync(
            "session-1",
            "user message",
            "assistant message",
            new List<(string ToolName, bool Success, string Content)>
            {
                ("tool-a", true, "tool content")
            });

        Assert.True(result.Successed);
        Assert.Equal(1, result.Data.TurnCount);

        var session = await db.AICreationSessions.AsNoTracking().SingleAsync(x => x.Id == "session-1");
        var messages = await db.AICreationMessages.AsNoTracking().ToListAsync();

        Assert.Equal(1, session.TurnCount);
        Assert.Equal(3, messages.Count);
        Assert.Contains(messages, x => x.Role == "user" && x.Content == "user message" && x.TurnNumber == 1);
        Assert.Contains(messages, x => x.Role == "assistant" && x.Content == "assistant message" && x.TurnNumber == 1);
        Assert.Contains(messages, x => x.Role == "tool" && x.ToolName == "tool-a" && x.ToolSuccess == true && x.TurnNumber == 1);
        Assert.Equal(("user-1", "work-1", "session-1", 1), memory.Refreshes.Single());
    }

    [Fact]
    public async Task AppendTurnAsync_RejectsInactiveSessionWithoutWritingMessages()
    {
        await using var db = TestDb.Create();
        var memory = new FakeMemoryProvider();
        var manager = CreateManager(db, memory);

        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-1",
            UserId = "user-1",
            WorkId = "work-1",
            Status = "paused",
            TurnCount = 2,
            AdoptedContentJson = "[]"
        });
        await db.SaveChangesAsync();

        var result = await manager.AppendTurnAsync("session-1", "user", "assistant");

        Assert.False(result.Successed);
        Assert.Empty(await db.AICreationMessages.AsNoTracking().ToListAsync());
        Assert.Empty(memory.Refreshes);
    }

    [Fact]
    public async Task AppendTurnAsync_QueuesMemoryRefreshWhenQueueIsConfigured()
    {
        await using var db = TestDb.Create();
        var memory = new FakeMemoryProvider();
        var queue = new RecordingMemoryRefreshQueue();
        var manager = CreateManager(db, memory, queue);

        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-1",
            UserId = "user-1",
            WorkId = "work-1",
            Status = "active",
            TurnCount = 0,
            AdoptedContentJson = "[]"
        });
        await db.SaveChangesAsync();

        var result = await manager.AppendTurnAsync("session-1", "user", "assistant");

        Assert.True(result.Successed);
        Assert.Empty(memory.Refreshes);
        var request = Assert.Single(queue.Requests);
        Assert.Equal("work-1", request.WorkId);
        Assert.Equal(1, request.TurnNumber);
    }

    private static CreationSessionManager CreateManager(
        SpeakEase.Write.Infrastructure.Persistence.SpeakEaseDbContext db,
        FakeMemoryProvider memory,
        IMemoryRefreshQueue queue = null)
    {
        return new CreationSessionManager(
            db,
            NullLogger<CreationSessionManager>.Instance,
            new TestUserContext("user-1"),
            memory,
            new SequentialIdGenerator(),
            queue);
    }

    private sealed class RecordingMemoryRefreshQueue : IMemoryRefreshQueue
    {
        public List<MemoryRefreshRequest> Requests { get; } = new();

        public ValueTask EnqueueAsync(MemoryRefreshRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.CompletedTask;
        }
    }
}
