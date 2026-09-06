using Moq;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Runtime;

namespace AINWZ.Tests.AI;

public sealed class RuntimeEventSinkTests
{
    [Fact]
    public async Task PublishAsync_PersistsRuntimeEventWithItsSequence()
    {
        var store = new Mock<IAgentRuntimeStore>();
        var sink = new AgentRuntimeEventSink(store.Object);
        var payload = new AgentStreamChunk { Type = "content", Content = "hello" };

        await sink.PublishAsync(new RuntimeEvent
        {
            RunId = "run-1",
            StepId = "step-1",
            Sequence = 9,
            Type = "model_chunk",
            Payload = payload
        });

        store.Verify(x => x.AppendEventAsync(
            "run-1",
            "step-1",
            9,
            "model_chunk",
            payload,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
