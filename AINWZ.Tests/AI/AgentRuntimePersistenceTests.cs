using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Runtime;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.AI;

public sealed class AgentRuntimePersistenceTests
{
    [Fact]
    public async Task SaveCheckpointAsync_DoesNotLetOlderVersionOverwriteNewerState()
    {
        await using var db = TestDb.Create();
        var store = new AgentRuntimeStore(
            new AgentRunStore(db, new TestUserContext(), new SequentialIdGenerator()),
            db,
            new TestUserContext(),
            new SequentialIdGenerator());

        await store.SaveCheckpointAsync(new AgentCheckpointDto
        {
            RunId = "run-1",
            StepId = "step-1",
            State = "tool_waiting",
            MessagesJson = "new",
            Version = 2
        });
        await store.SaveCheckpointAsync(new AgentCheckpointDto
        {
            RunId = "run-1",
            StepId = "step-1",
            State = "old",
            MessagesJson = "old",
            Version = 1
        });

        var loaded = await store.LoadCheckpointAsync("run-1", "step-1");

        Assert.Equal(2, loaded.Version);
        Assert.Equal("tool_waiting", loaded.State);
        Assert.Equal("new", loaded.MessagesJson);
        Assert.Equal(1, await db.AgentCheckpoints.CountAsync());
    }

    [Fact]
    public async Task SaveCheckpointAsync_ConcurrentNewerWriterCannotBeOverwritten()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var setup = new SpeakEaseDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.AgentCheckpoints.Add(new AgentCheckpointEntity
            {
                Id = "checkpoint-1",
                UserId = "user-1",
                RunId = "run-1",
                StepId = "step-1",
                State = "created",
                Version = 0,
                CreateBy = "user-1",
                UpdateBy = "user-1"
            });
            await setup.SaveChangesAsync();
        }

        await using var db = new CheckpointRacingDbContext(options);
        var store = new AgentRuntimeStore(
            new AgentRunStore(db, new TestUserContext(), new SequentialIdGenerator()),
            db,
            new TestUserContext(),
            new SequentialIdGenerator());

        await store.SaveCheckpointAsync(new AgentCheckpointDto
        {
            RunId = "run-1",
            StepId = "step-1",
            State = "older-writer",
            Version = 1
        });

        await using var verification = new SpeakEaseDbContext(options);
        var persisted = await verification.AgentCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(2, persisted.Version);
        Assert.Equal("newer-writer", persisted.State);
    }

    private sealed class CheckpointRacingDbContext : SpeakEaseDbContext
    {
        private readonly DbContextOptions<SpeakEaseDbContext> _options;
        private bool _racePending = true;

        public CheckpointRacingDbContext(DbContextOptions<SpeakEaseDbContext> options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var checkpoint = ChangeTracker.Entries<AgentCheckpointEntity>()
                .FirstOrDefault(x => x.State == EntityState.Modified && x.Entity.Version == 1);
            if (_racePending && checkpoint is not null)
            {
                _racePending = false;
                await using var concurrent = new SpeakEaseDbContext(_options);
                await concurrent.AgentCheckpoints
                    .Where(x => x.Id == checkpoint.Entity.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Version, 2)
                        .SetProperty(x => x.State, "newer-writer"), cancellationToken);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
