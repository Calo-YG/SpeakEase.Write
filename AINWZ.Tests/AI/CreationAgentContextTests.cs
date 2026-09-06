using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Application.Abstractions.Story;
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

    [Fact]
    public async Task BuildContextAsync_OrdersUserBeforeAssistantWhenTimestampsTie()
    {
        await using var db = TestDb.Create();
        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-order",
            WorkId = "work-1",
            UserId = "user-1",
            Status = "active"
        });
        var timestamp = DateTime.UtcNow;
        db.AICreationMessages.Add(new AICreationMessageEntity
        {
            Id = "assistant-first",
            SessionId = "session-order",
            Role = "assistant",
            Content = "answer",
            TurnNumber = 1,
            CreatedAt = timestamp
        });
        db.AICreationMessages.Add(new AICreationMessageEntity
        {
            Id = "user-second",
            SessionId = "session-order",
            Role = "user",
            Content = "question",
            TurnNumber = 1,
            CreatedAt = timestamp
        });
        await db.SaveChangesAsync();
        var contextBuilder = new CreationAgentContext(
            new FakeMemoryProvider(),
            new TestUserContext(),
            db,
            new SequentialIdGenerator());

        var context = await contextBuilder.BuildContextAsync(
            "work-1", "session-order", "general", "test-model",
            includeMemory: false, filterHistory: false, contextWindowTokens: 32_000);

        Assert.IsType<UserMessage>(context.ConversationHistory[0]);
        Assert.IsType<AssistantMessage>(context.ConversationHistory[1]);
    }

    [Fact]
    public async Task BuildContextAsync_IncludesConfirmedCharacterRuntimeStateForWork()
    {
        await using var db = TestDb.Create();
        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-character",
            WorkId = "work-1",
            UserId = "user-1",
            Status = "active"
        });
        await db.SaveChangesAsync();
        var characterStore = new Mock<ICharacterStateStore>();
        characterStore
            .Setup(x => x.GetWorkSnapshotsAsync("user-1", "work-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CharacterStateSnapshotData
                {
                    CharacterId = "character-1",
                    WorkId = "work-1",
                    UserId = "user-1",
                    Version = 4,
                    Status = "confirmed",
                    StateJson = "{\"personality\":\"克制\",\"plotHooks\":[\"隐瞒旧伤\"]}"
                }
            });
        var contextBuilder = new CreationAgentContext(
            new FakeMemoryProvider(),
            new TestUserContext(),
            db,
            new SequentialIdGenerator(),
            characterStateStore: characterStore.Object);

        var context = await contextBuilder.BuildContextAsync(
            "work-1", "session-character", "write", "test-model",
            includeMemory: true, filterHistory: true, contextWindowTokens: 32_000);

        var runtimeMessage = Assert.Single(
            context.ConversationHistory.OfType<SystemMessage>(),
            x => x.Content.Contains("[Character Runtime]"));
        Assert.Contains("character-1", runtimeMessage.Content);
        Assert.Contains("隐瞒旧伤", runtimeMessage.Content);
    }

    [Fact]
    public async Task BuildContextAsync_WritesUtcAssemblyAuditTimestamp()
    {
        await using var db = TestDb.Create();
        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-audit",
            WorkId = "work-1",
            UserId = "user-1",
            Status = "active"
        });
        await db.SaveChangesAsync();
        var contextBuilder = new CreationAgentContext(
            new FakeMemoryProvider(),
            new TestUserContext(),
            db,
            new SequentialIdGenerator());

        await contextBuilder.BuildContextAsync(
            "work-1", "session-audit", "general", "test-model",
            includeMemory: false, filterHistory: true, contextWindowTokens: 32_000);

        var audit = Assert.Single(db.ContextAssemblyLogs);
        Assert.Equal(DateTimeKind.Utc, audit.CreateAt.Kind);
        Assert.Equal(DateTimeKind.Utc, audit.UpdateAt.Kind);
    }
}
