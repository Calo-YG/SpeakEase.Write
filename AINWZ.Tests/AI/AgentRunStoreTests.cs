using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.AI.Runtime;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.AI;

public sealed class AgentRunStoreTests
{
    [Fact]
    public async Task StartAsync_DeduplicatesAndCompletedRunCanBeReplayed()
    {
        await using var db = TestDb.Create();
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());

        var first = await store.StartAsync("work-1", "session-1", "idem-1", "client-1");
        var second = await store.StartAsync("work-1", "session-1", "idem-1", "client-1");

        Assert.Equal(first.RunId, second.RunId);
        Assert.True(second.IsInProgress);

        await store.CompleteAsync(first.RunId, new AgentResponse
        {
            Content = "answer",
            StopReason = "completed",
            Model = "test"
        });

        var replay = await store.StartAsync("work-1", "session-1", "idem-1", "client-1");
        Assert.True(replay.IsReplay);
        Assert.Equal("answer", replay.ExistingResponse.Content);
        Assert.Equal(1, await db.AgentRuns.CountAsync());
    }

    [Fact]
    public async Task StartAsync_DetachesFailedInsertAfterSimulatedUniqueKeyRace()
    {
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AgentRunRacingDbContext(options);
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());

        var result = await store.StartAsync("work-race", "session-race", "idem-race", "client-race");

        Assert.Equal("concurrent-run", result.RunId);
        Assert.True(result.IsInProgress);
        Assert.Single(await db.AgentRuns.AsNoTracking().ToListAsync());
        Assert.Empty(db.ChangeTracker.Entries<AgentRunEntity>());
    }

    [Fact]
    public async Task StartAsync_RethrowsInsertFailureWhenConcurrentRunIsMissing()
    {
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FailingAgentRunDbContext(options);
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            store.StartAsync("work-missing", "session-missing", "idem-missing", "client-missing"));

        Assert.Same(db.InsertFailure, exception);
        Assert.Empty(db.ChangeTracker.Entries<AgentRunEntity>());
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("cancelled")]
    [InlineData("timed_out")]
    public async Task StartAsync_ReacquiresFailedRunBeforeRetry(string stopReason)
    {
        await using var db = TestDb.Create();
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());
        var first = await store.StartAsync("work-retry", "session-retry", "idem-retry", "client-retry");
        await store.CompleteAsync(first.RunId, new AgentResponse
        {
            Content = "partial answer",
            StopReason = stopReason,
            Model = "test-model"
        });
        var terminal = await db.AgentRuns.SingleAsync();
        terminal.UpdateBy = "previous-user";
        terminal.UpdateAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();
        var failed = await db.AgentRuns.AsNoTracking().SingleAsync();

        var reacquired = await store.StartAsync("work-retry", "session-retry", "idem-retry", "client-retry");
        var inProgress = await store.StartAsync("work-retry", "session-retry", "idem-retry", "client-retry");

        Assert.Equal(first.RunId, reacquired.RunId);
        Assert.False(reacquired.IsReplay);
        Assert.False(reacquired.IsInProgress);
        Assert.Equal(first.RunId, inProgress.RunId);
        Assert.True(inProgress.IsInProgress);

        var recovered = await db.AgentRuns.AsNoTracking().SingleAsync();
        Assert.Equal("running", recovered.Status);
        Assert.Equal(string.Empty, recovered.StopReason);
        Assert.Equal(string.Empty, recovered.Content);
        Assert.Equal(string.Empty, recovered.ResultJson);
        Assert.Equal(string.Empty, recovered.Model);
        Assert.Null(recovered.CompletedAt);
        Assert.True(recovered.UpdateAt > failed.UpdateAt);
        Assert.Equal("user-1", recovered.UpdateBy);
    }

    [Fact]
    public async Task StartAsync_ReacquiresFailedRunAndContinuesRelationalEventSequence()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new SpeakEaseDbContext(options);
        await db.Database.EnsureCreatedAsync();
        Assert.True(db.Database.IsRelational());
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());
        var first = await store.StartAsync("work-sqlite", "session-sqlite", "idem-sqlite", "client-sqlite");
        await store.CompleteAsync(first.RunId, new AgentResponse
        {
            Content = "partial answer",
            StopReason = "failed",
            Model = "test-model"
        });
        await store.AppendEventAsync(first.RunId, "step-old", 1, "content", new { Content = "old" });
        var terminal = await db.AgentRuns.SingleAsync();
        terminal.UpdateBy = "previous-user";
        terminal.UpdateAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();
        var failed = await db.AgentRuns.AsNoTracking().SingleAsync();

        var reacquired = await store.StartAsync("work-sqlite", "session-sqlite", "idem-sqlite", "client-sqlite");
        await store.AppendEventAsync(
            first.RunId,
            "step-new",
            reacquired.LastEventSequence + 1,
            "content",
            new { Content = "new" });
        var inProgress = await store.StartAsync("work-sqlite", "session-sqlite", "idem-sqlite", "client-sqlite");

        Assert.Equal(first.RunId, reacquired.RunId);
        Assert.False(reacquired.IsReplay);
        Assert.False(reacquired.IsInProgress);
        Assert.Equal(1, reacquired.LastEventSequence);
        Assert.Equal(first.RunId, inProgress.RunId);
        Assert.True(inProgress.IsInProgress);
        var recovered = await db.AgentRuns.AsNoTracking().SingleAsync();
        Assert.Equal("running", recovered.Status);
        Assert.Equal(string.Empty, recovered.StopReason);
        Assert.Equal(string.Empty, recovered.Content);
        Assert.Equal(string.Empty, recovered.ResultJson);
        Assert.Equal(string.Empty, recovered.Model);
        Assert.Null(recovered.CompletedAt);
        Assert.True(recovered.UpdateAt > failed.UpdateAt);
        Assert.Equal("user-1", recovered.UpdateBy);
        Assert.Equal(new long[] { 1, 2 }, await db.AgentRunEvents.AsNoTracking()
            .OrderBy(x => x.Sequence)
            .Select(x => x.Sequence)
            .ToArrayAsync());
    }

    [Fact]
    public async Task StartAsync_LeavesRunTerminalWhenLastEventSequenceQueryIsCancelled()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var interceptor = new CancelMaxQueryInterceptor();
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using var db = new SpeakEaseDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());
        var first = await store.StartAsync("work-cancel", "session-cancel", "idem-cancel", "client-cancel");
        await store.CompleteAsync(first.RunId, new AgentResponse
        {
            Content = "partial answer",
            StopReason = "failed",
            Model = "test-model"
        });
        await store.AppendEventAsync(first.RunId, "step-old", 1, "content", new { Content = "old" });
        interceptor.CancelNextMaxQuery();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.StartAsync("work-cancel", "session-cancel", "idem-cancel", "client-cancel"));

        Assert.True(interceptor.MaxQueryIntercepted);
        await using var verificationDb = new SpeakEaseDbContext(options);
        var persisted = await verificationDb.AgentRuns.AsNoTracking().SingleAsync();
        Assert.Equal("failed", persisted.Status);
    }

    [Fact]
    public async Task ToolJournal_ReplaysCompletedCallAndDoesNotCreateDuplicateLease()
    {
        await using var db = TestDb.Create();
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());
        var run = await store.StartAsync("work-1", "session-1", "idem-tool", "client-tool");
        var call = new ToolCall
        {
            Id = "call-1",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{\"value\":1}" }
        };

        var firstLease = await store.BeginAsync(run.RunId, "step-1", "0:0", call);
        Assert.True(firstLease.ShouldExecute);
        await store.CompleteAsync(run.RunId, "step-1", "0:0", call, new ToolResult
        {
            Success = true,
            Content = "saved",
            ToolCallId = call.Id,
            ToolName = call.Function.Name
        });

        var replayLease = await store.BeginAsync(run.RunId, "step-1", "0:0", call);
        Assert.False(replayLease.ShouldExecute);
        Assert.Equal("saved", replayLease.ReplayResult.Content);
        Assert.Equal(1, await db.AgentToolCalls.CountAsync());
    }

    [Fact]
    public async Task ToolJournal_ReplaysRecoveredCallWhenModelChangesToolCallId()
    {
        await using var db = TestDb.Create();
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());
        var run = await store.StartAsync("work-1", "session-1", "idem-recovered-tool", "client-tool");
        var originalCall = new ToolCall
        {
            Id = "model-call-original",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{\"value\":1}" }
        };
        var recoveredCall = new ToolCall
        {
            Id = "model-call-recovered",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{\"value\":1}" }
        };

        var firstLease = await store.BeginAsync(run.RunId, "step-1", "0:0", originalCall);
        Assert.True(firstLease.ShouldExecute);
        await store.CompleteAsync(run.RunId, "step-1", "0:0", originalCall, new ToolResult
        {
            Success = true,
            Content = "saved",
            ToolCallId = originalCall.Id,
            ToolName = originalCall.Function.Name
        });

        var recoveredLease = await store.BeginAsync(run.RunId, "step-1", "0:0", recoveredCall);

        Assert.False(recoveredLease.ShouldExecute);
        Assert.Equal("saved", recoveredLease.ReplayResult.Content);
        Assert.Equal(1, await db.AgentToolCalls.CountAsync());

        recoveredCall.Function.Arguments = "{\"value\":2}";
        var conflictingLease = await store.BeginAsync(
            run.RunId, "step-1", "0:0", recoveredCall);
        var secondSlotLease = await store.BeginAsync(
            run.RunId, "step-1", "0:1", originalCall);

        Assert.False(conflictingLease.ShouldExecute);
        Assert.Equal("tool_call_identity_conflict", conflictingLease.ReplayResult.ErrorCode);
        Assert.True(secondSlotLease.ShouldExecute);
        Assert.Equal(2, await db.AgentToolCalls.CountAsync());
    }

    [Fact]
    public async Task ToolJournal_UsesIndependentExecutionSlotsForEachPlanStep()
    {
        await using var db = TestDb.Create();
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());
        var run = await store.StartAsync("work-1", "session-1", "idem-multi-step", "client-tool");
        var firstCall = new ToolCall
        {
            Id = "model-call-1",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{\"value\":1}" }
        };
        var secondCall = new ToolCall
        {
            Id = "model-call-2",
            Function = new FunctionCallDetail { Name = "update", Arguments = "{\"value\":2}" }
        };

        var firstLease = await store.BeginAsync(run.RunId, "step-1", "0:0", firstCall);
        var secondLease = await store.BeginAsync(run.RunId, "step-2", "0:0", secondCall);

        Assert.True(firstLease.ShouldExecute);
        Assert.True(secondLease.ShouldExecute);
        Assert.Equal(2, await db.AgentToolCalls.CountAsync());
    }

    [Fact]
    public async Task ToolJournal_ConvertsSaveRaceIntoInProgressLease()
    {
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new RacingDbContext(options);
        var store = new AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());
        var run = await store.StartAsync("work-1", "session-1", "idem-race", "client-race");
        var call = new ToolCall
        {
            Id = "call-race",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{}" }
        };

        var lease = await store.BeginAsync(run.RunId, "step-1", "0:0", call);

        Assert.False(lease.ShouldExecute);
        Assert.Equal("tool_call_in_progress", lease.ReplayResult.ErrorCode);
        Assert.Equal(1, await db.AgentToolCalls.CountAsync());
    }

    private sealed class RacingDbContext(DbContextOptions<SpeakEaseDbContext> options) : SpeakEaseDbContext(options)
    {
        private bool _simulateRace = true;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var hasToolInsert = ChangeTracker.Entries<SpeakEase.Write.Domain.Entities.AI.AgentToolCallEntity>()
                .Any(x => x.State == EntityState.Added);
            var affected = await base.SaveChangesAsync(cancellationToken);
            if (_simulateRace && hasToolInsert)
            {
                _simulateRace = false;
                throw new DbUpdateException("Simulated unique-key race.");
            }

            return affected;
        }
    }

    private sealed class AgentRunRacingDbContext : SpeakEaseDbContext
    {
        private readonly DbContextOptions<SpeakEaseDbContext> _options;
        private bool _simulateRace = true;

        public AgentRunRacingDbContext(DbContextOptions<SpeakEaseDbContext> options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var pending = ChangeTracker.Entries<AgentRunEntity>()
                .SingleOrDefault(x => x.State == EntityState.Added)?.Entity;
            if (_simulateRace && pending is not null)
            {
                _simulateRace = false;
                await using var concurrentDb = new SpeakEaseDbContext(_options);
                concurrentDb.AgentRuns.Add(new AgentRunEntity
                {
                    Id = "concurrent-run",
                    UserId = pending.UserId,
                    WorkId = pending.WorkId,
                    SessionId = pending.SessionId,
                    DeduplicationKey = pending.DeduplicationKey,
                    ClientMessageId = pending.ClientMessageId,
                    Status = "running",
                    StartedAt = pending.StartedAt,
                    CreateBy = pending.CreateBy,
                    CreateAt = pending.CreateAt,
                    UpdateBy = pending.UpdateBy,
                    UpdateAt = pending.UpdateAt
                });
                await concurrentDb.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("Simulated unique-key race.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class FailingAgentRunDbContext(DbContextOptions<SpeakEaseDbContext> options)
        : SpeakEaseDbContext(options)
    {
        public DbUpdateException InsertFailure { get; } = new("Simulated insert failure.");

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<AgentRunEntity>().Any(x => x.State == EntityState.Added))
                throw InsertFailure;

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class CancelMaxQueryInterceptor : DbCommandInterceptor
    {
        private bool _cancelNextMaxQuery;

        public bool MaxQueryIntercepted { get; private set; }

        public void CancelNextMaxQuery()
        {
            _cancelNextMaxQuery = true;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (_cancelNextMaxQuery && command.CommandText.Contains("MAX(", StringComparison.OrdinalIgnoreCase))
            {
                _cancelNextMaxQuery = false;
                MaxQueryIntercepted = true;
                throw new OperationCanceledException("Simulated MAX query cancellation.", cancellationToken);
            }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
