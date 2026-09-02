using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Runtime;

namespace AINWZ.Tests.AI;

public sealed class CompatibilityContractTests
{
    [Fact]
    public void ProjectToSse_PreservesLegacyEventTypesAndOrder()
    {
        var projector = new AgentEventProjector();
        var events = new[]
        {
            new AgentEvent
            {
                RunId = "run-1",
                StepId = "write",
                Sequence = 1,
                Type = "content",
                Payload = new AgentStreamChunk { Type = "content", Content = "draft" }
            },
            new AgentEvent
            {
                RunId = "run-1",
                StepId = "write",
                Sequence = 2,
                Type = "tool_result",
                Payload = new AgentStreamChunk
                {
                    Type = "tool_result",
                    ToolResult = new ToolResult
                    {
                        ToolName = "get_character",
                        Success = true,
                        Content = "ok"
                    }
                }
            },
            new AgentEvent
            {
                RunId = "run-1",
                StepId = "write",
                Sequence = 3,
                Type = "done",
                Payload = new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = new AgentResponse
                    {
                        Content = "完成",
                        StopReason = "completed"
                    }
                }
            }
        };

        var chunks = events.Select(projector.ProjectToSse).ToList();

        Assert.Equal(new[] { "content", "tool_result", "done" }, chunks.Select(x => x.Type));
        Assert.Equal(new long[] { 1, 2, 3 }, chunks.Select(x => x.Sequence));
        Assert.All(chunks, chunk => Assert.Equal("run-1", chunk.RunId));
        Assert.All(chunks, chunk => Assert.Equal("write", chunk.StepId));
        Assert.Equal("completed", chunks[^1].FinalResponse.StopReason);
    }
}
