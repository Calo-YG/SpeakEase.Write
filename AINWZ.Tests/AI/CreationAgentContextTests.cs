using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Memory;

namespace AINWZ.Tests.AI;

public sealed class CreationAgentContextTests
{
    [Fact]
    public async Task BuildContextAsync_LoadsRecentCompleteTurnsInsteadOfMessageCount()
    {
        await using var db = TestDb.Create();
        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-1",
            WorkId = "work-1",
            UserId = "user-1",
            Status = "active"
        });

        for (var turn = 1; turn <= 10; turn++)
        {
            db.AICreationMessages.Add(new AICreationMessageEntity
            {
                Id = $"user-{turn}",
                SessionId = "session-1",
                Role = "user",
                Content = $"turn-{turn}-user",
                TurnNumber = turn,
                CreatedAt = DateTime.Now.AddMinutes(turn)
            });
            db.AICreationMessages.Add(new AICreationMessageEntity
            {
                Id = $"assistant-{turn}",
                SessionId = "session-1",
                Role = "assistant",
                Content = $"turn-{turn}-assistant",
                TurnNumber = turn,
                CreatedAt = DateTime.Now.AddMinutes(turn).AddSeconds(1)
            });
        }
        await db.SaveChangesAsync();

        var contextBuilder = new CreationAgentContext(
            new FakeMemoryProvider(),
            new TestUserContext(),
            db,
            new SequentialIdGenerator());

        var context = await contextBuilder.BuildContextAsync(
            "work-1",
            "session-1",
            "general",
            "test-model",
            includeMemory: false,
            filterHistory: true,
            contextWindowTokens: 32_000);

        var contents = context.ConversationHistory
            .OfType<UserMessage>()
            .Select(x => (string)x.Content)
            .ToList();

        Assert.Equal(8, contents.Count);
        Assert.Equal("turn-3-user", contents[0]);
        Assert.Equal("turn-10-user", contents[^1]);
    }

    [Fact]
    public async Task BuildContextAsync_TrimsOversizedMemoryMessageWithinSmallContextWindow()
    {
        await using var db = TestDb.Create();
        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-1",
            WorkId = "work-1",
            UserId = "user-1",
            Status = "active"
        });
        await db.SaveChangesAsync();

        var memory = new FakeMemoryProvider
        {
            Snapshot = new SessionMemorySnapshot
            {
                SnapshotId = "snapshot-1",
                Summary = new string('中', 10_000),
                TurnNumber = 1
            }
        };
        var contextBuilder = new CreationAgentContext(
            memory,
            new TestUserContext(),
            db,
            new SequentialIdGenerator());

        var context = await contextBuilder.BuildContextAsync(
            "work-1",
            "session-1",
            "general",
            "test-model",
            includeMemory: true,
            filterHistory: true,
            contextWindowTokens: 6_000);

        Assert.True(context.WasTrimmed);
        Assert.InRange(context.InputTokenCount, 1, 4_500);
    }

    [Fact]
    public async Task BuildContextAsync_DoesNotExceedInputBudgetForTinyWindow()
    {
        await using var db = TestDb.Create();
        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-1",
            WorkId = "work-1",
            UserId = "user-1",
            Status = "active"
        });
        await db.SaveChangesAsync();

        var memory = new FakeMemoryProvider
        {
            Snapshot = new SessionMemorySnapshot
            {
                SnapshotId = "snapshot-1",
                Summary = new string('中', 10_000),
                TurnNumber = 1
            }
        };
        var contextBuilder = new CreationAgentContext(
            memory,
            new TestUserContext(),
            db,
            new SequentialIdGenerator());

        var context = await contextBuilder.BuildContextAsync(
            "work-1",
            "session-1",
            "general",
            "test-model",
            includeMemory: true,
            filterHistory: true,
            contextWindowTokens: 4);

        Assert.InRange(context.InputTokenCount, 0, 3);
    }
}
