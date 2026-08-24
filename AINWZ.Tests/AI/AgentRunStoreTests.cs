using Microsoft.EntityFrameworkCore;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.AI.Runtime;

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
}
