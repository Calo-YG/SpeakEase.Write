using Microsoft.EntityFrameworkCore;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
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

        var firstLease = await store.BeginAsync(run.RunId, "step-1", call);
        Assert.True(firstLease.ShouldExecute);
        await store.CompleteAsync(run.RunId, "step-1", call, new ToolResult
        {
            Success = true,
            Content = "saved",
            ToolCallId = call.Id,
            ToolName = call.Function.Name
        });

        var replayLease = await store.BeginAsync(run.RunId, "step-1", call);
        Assert.False(replayLease.ShouldExecute);
        Assert.Equal("saved", replayLease.ReplayResult.Content);
        Assert.Equal(1, await db.AgentToolCalls.CountAsync());
    }
}
