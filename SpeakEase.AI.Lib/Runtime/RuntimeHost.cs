using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Runtime;

public sealed class RuntimeHost(
    IAgentLoop agentLoop,
    IRuntimeEventSink eventSink = null)
{
    private readonly IAgentLoop _agentLoop = agentLoop ?? throw new ArgumentNullException(nameof(agentLoop));
    private readonly IRuntimeEventSink _eventSink = eventSink;
    private long _sequence;
    private bool _publishEvents;
    private readonly List<RuntimeTransition> _transitions = new();

    public RuntimeState State { get; private set; } = RuntimeState.Created;
    public IReadOnlyList<RuntimeTransition> Transitions => _transitions;

    public async IAsyncEnumerable<RuntimeEvent> RunAsync(
        RuntimeRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.LoopRequest);
        _sequence = 0;
        _publishEvents = request.PublishEvents;
        _transitions.Clear();
        State = RuntimeState.Created;
        TransitionTo(RuntimeState.Running, "run_started");

        await foreach (var runtimeEvent in EmitLifecycleAsync(
            request.LoopRequest,
            "run_started",
            null,
            cancellationToken))
        {
            yield return runtimeEvent;
        }

        AgentResponse finalResponse = null;
        OperationCanceledException cancellation = null;
        await using (var enumerator = _agentLoop.RunAsync(request.LoopRequest, cancellationToken)
                         .GetAsyncEnumerator(cancellationToken))
        {
            while (true)
            {
                AgentEvent eventItem = null;
                var hasNext = false;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (hasNext)
                        eventItem = enumerator.Current;
                }
                catch (OperationCanceledException ex)
                {
                    cancellation = ex;
                }

                if (cancellation is not null || !hasNext)
                    break;

                var projected = CreateEvent(request.LoopRequest, eventItem.Type, eventItem.Chunk ?? eventItem.Payload);
                if (projected.Chunk?.Type == "tool_call")
                    TransitionTo(RuntimeState.WaitingTool, "tool_call");
                else if (projected.Chunk?.Type == "tool_result")
                    TransitionTo(RuntimeState.Running, "tool_result");
                if (projected.Chunk?.Type == "done")
                    finalResponse = projected.Chunk.FinalResponse;

                await PublishAsync(projected, cancellationToken);
                yield return projected;
            }
        }

        if (cancellation is not null)
        {
            var cancelledByCaller = cancellationToken.IsCancellationRequested;
            var stopReason = cancelledByCaller ? "cancelled" : "timed_out";
            TransitionTo(cancelledByCaller ? RuntimeState.Cancelled : RuntimeState.TimedOut, stopReason);
            finalResponse = new AgentResponse
            {
                Content = string.Empty,
                StopReason = stopReason,
                RunStatus = stopReason
            };
            var cancellationTerminal = CreateEvent(
                request.LoopRequest,
                cancelledByCaller ? "run_cancelled" : "run_timed_out",
                finalResponse);
            await PublishAsync(cancellationTerminal, CancellationToken.None);
            if (cancelledByCaller)
                ExceptionDispatchInfo.Capture(cancellation).Throw();

            yield return cancellationTerminal;
            yield break;
        }

        TransitionTo(MapState(finalResponse?.StopReason), finalResponse?.StopReason ?? "missing_terminal_response");
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
        if (_publishEvents && _eventSink is not null)
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

    private void TransitionTo(RuntimeState next, string reason)
    {
        if (State == next)
            return;

        _transitions.Add(new RuntimeTransition
        {
            From = State,
            To = next,
            Reason = reason ?? string.Empty
        });
        State = next;
    }
}
