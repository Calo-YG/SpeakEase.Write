using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Runtime;

public sealed class AgentRuntimeRunner(
    RuntimeHost host,
    IStepScheduler scheduler = null,
    IRuntimeEventSink eventSink = null,
    IRuntimeStateStore stateStore = null) : IAgentRuntimeRunner
{
    private readonly RuntimeHost _host = host ?? throw new ArgumentNullException(nameof(host));
    private readonly IStepScheduler _scheduler = scheduler ?? new LinearStepScheduler();
    private readonly IRuntimeEventSink _eventSink = eventSink;
    private readonly IRuntimeStateStore _stateStore = stateStore;

    public IAsyncEnumerable<RuntimeEvent> RunAsync(
        RuntimeRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.LoopRequest);
        if (request.Context is not null &&
            (!string.Equals(request.Context.RunId, request.LoopRequest.RunId, StringComparison.Ordinal) ||
             !string.Equals(request.Context.StepId, request.LoopRequest.StepId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("RunContext must match the AgentLoop run and step identifiers.");
        }

        return _host.RunAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<RuntimeEvent> RunPlanAsync(
        RuntimePlanRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);
        if (string.IsNullOrWhiteSpace(request.Context.RunId))
            throw new InvalidOperationException("Runtime plan requires a run id.");

        var ordered = _scheduler.Order(request.Steps);
        var artifacts = new Dictionary<string, RuntimeArtifact>(StringComparer.OrdinalIgnoreCase);
        long sequence = 0;
        var planStarted = CreateEvent(request.Context.RunId, "runtime", ref sequence, "plan_started", null);
        await PublishAsync(planStarted, request.PublishEvents, cancellationToken);
        yield return planStarted;

        foreach (var step in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stepStarted = CreateEvent(request.Context.RunId, step.Id, ref sequence, "step_started", null);
            await PublishAsync(stepStarted, request.PublishEvents, cancellationToken);
            yield return stepStarted;
            await SaveCheckpointAsync(request.Context.RunId, step.Id, "running", 1, null, cancellationToken);

            var runRequest = step.CreateRequest(artifacts);
            ValidateStepRequest(request.Context.RunId, step.Id, runRequest);
            runRequest = new RuntimeRunRequest
            {
                Context = runRequest.Context,
                LoopRequest = runRequest.LoopRequest,
                Options = runRequest.Options,
                PublishEvents = false
            };

            AgentResponse finalResponse = null;
            OperationCanceledException cancellation = null;
            await using (var enumerator = _host.RunAsync(runRequest, cancellationToken)
                             .GetAsyncEnumerator(cancellationToken))
            {
                while (true)
                {
                    RuntimeEvent current = null;
                    var hasNext = false;
                    try
                    {
                        hasNext = await enumerator.MoveNextAsync();
                        if (hasNext)
                            current = enumerator.Current;
                    }
                    catch (OperationCanceledException ex)
                    {
                        cancellation = ex;
                    }

                    if (cancellation is not null || !hasNext)
                        break;

                    if (current.Chunk?.FinalResponse is not null)
                        finalResponse = current.Chunk.FinalResponse;
                    else if (current.Payload is AgentResponse terminalResponse)
                        finalResponse = terminalResponse;
                    var projected = ReSequence(current, request.Context.RunId, step.Id, ref sequence);
                    await PublishAsync(projected, request.PublishEvents, cancellationToken);
                    yield return projected;
                }
            }

            if (cancellation is not null)
            {
                var cancelledByCaller = cancellationToken.IsCancellationRequested;
                var terminalState = cancelledByCaller ? "cancelled" : "timed_out";
                await SaveCheckpointAsync(
                    request.Context.RunId,
                    step.Id,
                    terminalState,
                    2,
                    finalResponse,
                    CancellationToken.None);
                var terminalResponse = finalResponse ?? new AgentResponse
                {
                    Content = string.Empty,
                    StopReason = terminalState,
                    RunStatus = terminalState
                };
                var stepCancelled = CreateEvent(
                    request.Context.RunId,
                    step.Id,
                    ref sequence,
                    cancelledByCaller ? "step_cancelled" : "step_timed_out",
                    terminalResponse);
                await PublishAsync(stepCancelled, request.PublishEvents, CancellationToken.None);
                yield return stepCancelled;
                var planCancelled = CreateEvent(
                    request.Context.RunId,
                    "runtime",
                    ref sequence,
                    cancelledByCaller ? "plan_cancelled" : "plan_timed_out",
                    terminalResponse);
                await PublishAsync(planCancelled, request.PublishEvents, CancellationToken.None);
                yield return planCancelled;
                ExceptionDispatchInfo.Capture(cancellation).Throw();
            }

            finalResponse ??= new AgentResponse { StopReason = "llm_error", RunStatus = "failed" };
            var stopReason = finalResponse.StopReason ?? "llm_error";
            await SaveCheckpointAsync(
                request.Context.RunId,
                step.Id,
                stopReason,
                2,
                finalResponse,
                CancellationToken.None);

            if (stopReason == "completed")
            {
                var content = finalResponse.Content ?? string.Empty;
                var artifact = new RuntimeArtifact
                {
                    RunId = request.Context.RunId,
                    StepId = step.Id,
                    ContentType = step.ContentType,
                    Summary = content.Length > 240 ? content[..240] : content,
                    Content = content,
                    EstimatedTokens = Math.Max(1, content.Length / 4)
                };
                artifacts[step.Id] = artifact;
                if (_stateStore is not null)
                    await _stateStore.SaveArtifactAsync(artifact, CancellationToken.None);
            }

            var stepTerminalType = stopReason == "completed" ? "step_completed" : "step_failed";
            var stepTerminal = CreateEvent(
                request.Context.RunId,
                step.Id,
                ref sequence,
                stepTerminalType,
                finalResponse);
            await PublishAsync(stepTerminal, request.PublishEvents, CancellationToken.None);
            yield return stepTerminal;
            if (stopReason != "completed")
            {
                var planFailed = CreateEvent(
                    request.Context.RunId,
                    "runtime",
                    ref sequence,
                    "plan_failed",
                    finalResponse);
                await PublishAsync(planFailed, request.PublishEvents, CancellationToken.None);
                yield return planFailed;
                yield break;
            }
        }

        var planCompleted = CreateEvent(request.Context.RunId, "runtime", ref sequence, "plan_completed", null);
        await PublishAsync(planCompleted, request.PublishEvents, CancellationToken.None);
        yield return planCompleted;
    }

    private async Task SaveCheckpointAsync(
        string runId,
        string stepId,
        string state,
        long version,
        AgentResponse response,
        CancellationToken cancellationToken)
    {
        if (_stateStore is null)
            return;

        await _stateStore.SaveCheckpointAsync(new RuntimeCheckpoint
        {
            RunId = runId,
            StepId = stepId,
            State = state,
            MessagesJson = response?.ConversationHistory is { Count: > 0 }
                ? JsonSerializer.Serialize(response.ConversationHistory)
                : string.Empty,
            Iteration = response?.Iterations ?? 0,
            Version = version
        }, cancellationToken);
    }

    private async Task PublishAsync(RuntimeEvent runtimeEvent, bool publish, CancellationToken cancellationToken)
    {
        if (publish && _eventSink is not null)
            await _eventSink.PublishAsync(runtimeEvent, cancellationToken);
    }

    private static void ValidateStepRequest(string runId, string stepId, RuntimeRunRequest request)
    {
        if (request?.LoopRequest is null ||
            !string.Equals(request.LoopRequest.RunId, runId, StringComparison.Ordinal) ||
            !string.Equals(request.LoopRequest.StepId, stepId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Runtime step request must match the plan run and step identifiers.");
        }
    }

    private static RuntimeEvent ReSequence(
        RuntimeEvent runtimeEvent,
        string runId,
        string stepId,
        ref long sequence)
    {
        var next = ++sequence;
        if (runtimeEvent.Chunk is { } chunk)
        {
            chunk.RunId = runId;
            chunk.StepId = stepId;
            chunk.Sequence = next;
        }

        return new RuntimeEvent
        {
            RunId = runId,
            StepId = stepId,
            Sequence = next,
            Type = runtimeEvent.Type,
            Payload = runtimeEvent.Payload,
            CreatedAt = runtimeEvent.CreatedAt
        };
    }

    private static RuntimeEvent CreateEvent(
        string runId,
        string stepId,
        ref long sequence,
        string type,
        object payload)
        => new()
        {
            RunId = runId,
            StepId = stepId,
            Sequence = ++sequence,
            Type = type,
            Payload = payload
        };
}
