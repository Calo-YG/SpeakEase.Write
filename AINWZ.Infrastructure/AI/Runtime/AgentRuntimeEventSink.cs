using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;

namespace SpeakEase.Write.Infrastructure.AI.Runtime;

public sealed class AgentRuntimeEventSink(IAgentRuntimeStore store) : IRuntimeEventSink
{
    private readonly IAgentRuntimeStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);
        return _store.AppendEventAsync(
            runtimeEvent.RunId,
            runtimeEvent.StepId,
            runtimeEvent.Sequence,
            runtimeEvent.Type,
            runtimeEvent.Payload,
            cancellationToken);
    }
}
