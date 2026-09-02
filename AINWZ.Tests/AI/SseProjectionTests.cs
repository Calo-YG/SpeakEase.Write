using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Infrastructure.AI.Runtime;

namespace AINWZ.Tests.AI;

public sealed class SseProjectionTests
{
    [Fact]
    public void Project_ModelChunk_PreservesLegacyContentChunk()
    {
        var projected = new AgentEventSseProjector().Project(new RuntimeEvent
        {
            RunId = "run-1",
            StepId = "step-1",
            Sequence = 4,
            Type = "model_chunk",
            Payload = new AgentStreamChunk { Type = "content", Content = "hello" }
        });

        Assert.Equal("content", projected.Type);
        Assert.Equal("hello", projected.Content);
        Assert.Equal("run-1", projected.RunId);
        Assert.Equal("step-1", projected.StepId);
        Assert.Equal(4, projected.Sequence);
    }

    [Fact]
    public void Project_RunCompleted_EmitsLegacyDoneChunk()
    {
        var projected = new AgentEventSseProjector().Project(new RuntimeEvent
        {
            RunId = "run-1",
            StepId = "step-1",
            Sequence = 5,
            Type = "run_completed",
            Payload = new AgentResponse { Content = "done", StopReason = "completed" }
        });

        Assert.Equal("done", projected.Type);
        Assert.Equal("done", projected.FinalResponse.Content);
        Assert.Equal("completed", projected.FinalResponse.StopReason);
    }
}
