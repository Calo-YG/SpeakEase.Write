using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    [Fact]
    public async Task AppendTurnAsync_DoesNotFailWhenSynchronousMemoryRefreshFails()
    {
        await using var db = TestDb.Create();
        var memory = new FakeMemoryProvider { ThrowOnRefresh = true };
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

        var result = await manager.AppendTurnAsync("session-1", "user", "assistant");

        Assert.True(result.Successed);
        Assert.Equal(1, result.Data.TurnCount);
        Assert.Equal(2, await db.AICreationMessages.CountAsync());
    }

    [Fact]
    public async Task RollbackToTurnAsync_RollsBackDatabaseChangesWhenSessionUpdateFails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var interceptor = new FailSessionUpdateInterceptor();
        var options = new DbContextOptionsBuilder<SpeakEase.Write.Infrastructure.Persistence.SpeakEaseDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using var db = new SpeakEase.Write.Infrastructure.Persistence.SpeakEaseDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var manager = CreateManager(db, new FakeMemoryProvider());
        const string originalAdoptedContent =
            "[{\"turnNumber\":1,\"content\":\"keep\",\"summary\":\"first\",\"adoptedAt\":\"2026-08-25T10:00:00\"}," +
            "{\"turnNumber\":2,\"content\":\"remove\",\"summary\":\"second\",\"adoptedAt\":\"2026-08-25T11:00:00\"}]";

        var now = new DateTime(2026, 8, 25, 12, 0, 0);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ai_creation_sessions"
                ("Id", "CreateBy", "CreateAt", "UpdateBy", "UpdateAt", "UserId", "WorkId", "Status",
                 "TurnCount", "AdoptedContentJson", "StartedAt", "LastActivityAt", "ExpiresAt", "CloseReason", "xmin")
            VALUES
                ({"session-rollback"}, {string.Empty}, {now}, {string.Empty}, {now}, {"user-1"}, {"work-rollback"}, {"active"},
                 {2}, {originalAdoptedContent}, {now}, {now}, {now.AddHours(24)}, {string.Empty}, {1u})
            """);
        db.AICreationMessages.AddRange(
            new AICreationMessageEntity
            {
                Id = "rollback-turn-1",
                SessionId = "session-rollback",
                TurnNumber = 1,
                Role = "user",
                Content = "one"
            },
            new AICreationMessageEntity
            {
                Id = "rollback-turn-2",
                SessionId = "session-rollback",
                TurnNumber = 2,
                Role = "user",
                Content = "two"
            });
        await db.SaveChangesAsync();
        interceptor.Arm();

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            manager.RollbackToTurnAsync("session-rollback", 1));

        Assert.Same(interceptor.UpdateFailure, exception.InnerException);
        Assert.True(
            interceptor.MessageDeleteIntercepted,
            $"Observed commands:{Environment.NewLine}{string.Join(Environment.NewLine, interceptor.ObservedCommands)}");
        Assert.True(interceptor.SessionUpdateIntercepted);
        Assert.True(interceptor.MessageDeleteObservedBeforeSessionUpdate);
        db.ChangeTracker.Clear();
        await using var verificationDb = new SpeakEase.Write.Infrastructure.Persistence.SpeakEaseDbContext(options);
        var persistedSession = await verificationDb.AICreationSessions
            .AsNoTracking()
            .SingleAsync(x => x.Id == "session-rollback");
        var persistedMessages = await verificationDb.AICreationMessages
            .AsNoTracking()
            .Where(x => x.SessionId == "session-rollback")
            .OrderBy(x => x.TurnNumber)
            .ToListAsync();

        Assert.Equal(2, persistedSession.TurnCount);
        Assert.Equal(originalAdoptedContent, persistedSession.AdoptedContentJson);
        Assert.Equal(new[] { 1, 2 }, persistedMessages.Select(x => x.TurnNumber));
    }

    [Fact]
    public async Task RollbackToTurnAsync_DoesNotFailWhenMemoryCleanupFails()
    {
        await using var db = TestDb.Create();
        var memory = new FakeMemoryProvider { ThrowOnRefresh = true };
        var manager = CreateManager(db, memory);

        db.AICreationSessions.Add(new AICreationSessionEntity
        {
            Id = "session-1",
            UserId = "user-1",
            WorkId = "work-1",
            Status = "active",
            TurnCount = 2,
            AdoptedContentJson = "[]"
        });
        db.AICreationMessages.AddRange(
            new AICreationMessageEntity { Id = "turn-1", SessionId = "session-1", TurnNumber = 1, Role = "user", Content = "one" },
            new AICreationMessageEntity { Id = "turn-2", SessionId = "session-1", TurnNumber = 2, Role = "user", Content = "two" });
        await db.SaveChangesAsync();

        var result = await manager.RollbackToTurnAsync("session-1", 1);

        Assert.True(result.Successed);
        Assert.Equal(1, await db.AICreationSessions.Select(x => x.TurnCount).SingleAsync());
        Assert.Single(await db.AICreationMessages.ToListAsync());
        Assert.Equal(("user-1", "work-1", "session-1", 1), Assert.Single(memory.Prunes));
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

    private sealed class FailSessionUpdateInterceptor : DbCommandInterceptor
    {
        private bool _armed;

        public InvalidOperationException UpdateFailure { get; } =
            new("Simulated creation session update failure.");

        public bool MessageDeleteIntercepted { get; private set; }

        public bool SessionUpdateIntercepted { get; private set; }

        public bool MessageDeleteObservedBeforeSessionUpdate { get; private set; }

        public List<string> ObservedCommands { get; } = new();

        public void Arm()
        {
            _armed = true;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ObserveCommand(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ObserveCommand(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void ObserveCommand(DbCommand command)
        {
            if (!_armed)
                return;

            ObservedCommands.Add(command.CommandText);

            var commandText = command.CommandText.TrimStart();
            if (commandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("ai_creation_messages", StringComparison.OrdinalIgnoreCase))
            {
                MessageDeleteIntercepted = true;
            }

            if (commandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("ai_creation_sessions", StringComparison.OrdinalIgnoreCase))
            {
                SessionUpdateIntercepted = true;
                MessageDeleteObservedBeforeSessionUpdate = MessageDeleteIntercepted;
                throw UpdateFailure;
            }
        }
    }
}
