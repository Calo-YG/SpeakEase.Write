using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using AINWZ.Tests.AI;

using SpeakEase.Write.Application.Applications;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.Application;

public sealed class WorkDeletionTests
{
    [Fact]
    public async Task DeleteWorkAsync_RemovesAgentRuntimeAndMemoryFactData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new SpeakEaseDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Works.Add(new WorkEntity { Id = "work-1", UserId = "user-1", Title = "Test" });
        db.MemoryFacts.Add(new MemoryFactEntity
        {
            Id = "fact-1",
            UserId = "user-1",
            WorkId = "work-1",
            SessionId = "session-1",
            Category = "character",
            Key = "hero"
        });
        db.AgentRuns.Add(new AgentRunEntity
        {
            Id = "run-1",
            UserId = "user-1",
            WorkId = "work-1",
            SessionId = "session-1",
            DeduplicationKey = "message-1"
        });
        db.AgentRunEvents.Add(new AgentRunEventEntity
        {
            Id = "event-1",
            UserId = "user-1",
            RunId = "run-1",
            Sequence = 1,
            Type = "completed"
        });
        db.AgentToolCalls.Add(new AgentToolCallEntity
        {
            Id = "tool-1",
            UserId = "user-1",
            RunId = "run-1",
            ToolCallId = "call-1",
            ToolName = "save",
            ArgumentsHash = "hash"
        });
        db.AgentArtifacts.Add(new AgentArtifactEntity
        {
            Id = "artifact-1",
            UserId = "user-1",
            RunId = "run-1",
            StepId = "write"
        });
        await db.SaveChangesAsync();
        var application = new WorkApplication(
            db,
            new SequentialIdGenerator(),
            new TestUserContext("user-1"),
            NullLogger<WorkApplication>.Instance);

        var result = await application.DeleteWorkAsync("work-1");

        Assert.True(result.Successed, result.Message);
        Assert.Empty(await db.MemoryFacts.AsNoTracking().ToListAsync());
        Assert.Empty(await db.AgentRunEvents.AsNoTracking().ToListAsync());
        Assert.Empty(await db.AgentToolCalls.AsNoTracking().ToListAsync());
        Assert.Empty(await db.AgentArtifacts.AsNoTracking().ToListAsync());
        Assert.Empty(await db.AgentRuns.AsNoTracking().ToListAsync());
    }
}
