using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Runtime;

public sealed class RuntimeHost(
    IAgentLoop agentLoop,
    IRuntimeEventSink eventSink = null)
{
    private readonly IAgentLoop _agentLoop = agentLoop ?? throw new ArgumentNullException(nameof(agentLoop));
    private readonly IRuntimeEventSink _eventSink = eventSink;
    private long _sequence;

    public RuntimeState State { get; private set; } = RuntimeState.Created;

    public async IAsyncEnumerable<RuntimeEvent> RunAsync(
        RuntimeRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.LoopRequest);
        _sequence = 0;
        State = RuntimeState.Running;

        await foreach (var runtimeEvent in EmitLifecycleAsync(
            request.LoopRequest,
            "run_started",
            null,
            cancellationToken))
        {
            yield return runtimeEvent;
        }

        AgentResponse finalResponse = null;
        await foreach (var eventItem in _agentLoop.RunAsync(request.LoopRequest, cancellationToken))
        {
            var projected = CreateEvent(request.LoopRequest, eventItem.Type, eventItem.Chunk ?? eventItem.Payload);
            if (projected.Chunk?.Type == "done")
                finalResponse = projected.Chunk.FinalResponse;

            await PublishAsync(projected, cancellationToken);
            yield return projected;
        }

        State = MapState(finalResponse?.StopReason);
        var terminalType = State == RuntimeState.Completed ? "run_completed" : "run_failed";
        var terminal = await EmitSingleLifecycleAsync(request.LoopRequest, terminalType, finalResponse);
        yield return terminal;
    }

    private async IAsyncEnumerable<RuntimeEvent> EmitLifecycleAsync(
        AgentLoopRequest request,
        string type,
        object payload,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var runtimeEvent = CreateEvent(request, type, payload);
        await PublishAsync(runtimeEvent, cancellationToken);
        yield return runtimeEvent;
    }

    private async Task<RuntimeEvent> EmitSingleLifecycleAsync(
        AgentLoopRequest request,
        string type,
        object payload)
    {
        await foreach (var runtimeEvent in EmitLifecycleAsync(request, type, payload, CancellationToken.None))
            return runtimeEvent;

        throw new InvalidOperationException("A lifecycle event was not emitted.");
    }

    private RuntimeEvent CreateEvent(AgentLoopRequest request, string type, object payload)
    {
        var sequence = ++_sequence;
        if (payload is AgentStreamChunk chunk)
        {
            chunk.RunId = request.RunId ?? string.Empty;
            chunk.StepId = request.StepId ?? string.Empty;
            chunk.Sequence = sequence;
        }

        return new RuntimeEvent
        {
            RunId = request.RunId ?? string.Empty,
            StepId = request.StepId ?? string.Empty,
            Sequence = sequence,
            Type = type ?? string.Empty,
            Payload = payload
        };
    }

    private async Task PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken)
    {
        if (_eventSink is not null)
            await _eventSink.PublishAsync(runtimeEvent, cancellationToken);
    }

    private static RuntimeState MapState(string stopReason)
    {
        return stopReason switch
        {
            "completed" => RuntimeState.Completed,
            "cancelled" => RuntimeState.Cancelled,
            "timed_out" => RuntimeState.TimedOut,
            "max_iterations_reached" => RuntimeState.MaxIterationsReached,
            _ when string.IsNullOrWhiteSpace(stopReason) => RuntimeState.Failed,
            _ => RuntimeState.Failed
        };
    }
}
