using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Runtime;

namespace AINWZ.Tests.AI;

public sealed class AgentEventProjectorTests
{
    [Fact]
    public void ProjectToSse_PreservesRuntimeIdentityAndLegacyPayload()
    {
        var source = new AgentStreamChunk { Type = "content", Content = "hello" };
        var runtimeEvent = new AgentEvent
        {
            RunId = "run-1",
            StepId = "step-1",
            Sequence = 7,
            Type = "content",
            Payload = source
        };

        var projected = new AgentEventProjector().ProjectToSse(runtimeEvent);

        Assert.Same(source, projected);
        Assert.Equal("run-1", projected.RunId);
        Assert.Equal("step-1", projected.StepId);
        Assert.Equal(7, projected.Sequence);
    }
}
