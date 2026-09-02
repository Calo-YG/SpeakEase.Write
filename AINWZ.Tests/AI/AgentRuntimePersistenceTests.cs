using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Runtime;

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
}
