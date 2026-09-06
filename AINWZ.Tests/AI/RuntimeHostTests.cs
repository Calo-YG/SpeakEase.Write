using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Runtime;

namespace AINWZ.Tests.AI;

public sealed class RuntimeHostTests
{
    [Fact]
    public async Task RunAsync_EmitsLifecycleEventsAndCompletedState()
    {
        var loop = new FakeAgentLoop(new[]
        {
            new AgentEvent
            {
                RunId = "run-1",
                StepId = "step-1",
                Sequence = 1,
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
        });
        var sink = new CapturingEventSink();
        var host = new RuntimeHost(loop, sink);

        var events = await CollectAsync(host.RunAsync(CreateRequest()));

        Assert.Equal(new[] { "run_started", "done", "run_completed" }, events.Select(x => x.Type));
        Assert.Equal(RuntimeState.Completed, host.State);
        Assert.Equal(events.Count, sink.Events.Count);
        Assert.Equal(new long[] { 1, 2, 3 }, events.Select(x => x.Sequence));
    }

    [Fact]
    public async Task RunAsync_MapsMaxIterationsToTerminalState()
    {
        var loop = new FakeAgentLoop(new[]
        {
            new AgentEvent
            {
                RunId = "run-1",
                StepId = "step-1",
                Sequence = 1,
                Type = "done",
                Payload = new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = new AgentResponse
                    {
                        StopReason = "max_iterations_reached"
                    }
                }
            }
        });
        var host = new RuntimeHost(loop);

        await CollectAsync(host.RunAsync(CreateRequest()));

        Assert.Equal(RuntimeState.MaxIterationsReached, host.State);
    }

    [Fact]
    public async Task RunAsync_CanSuppressNestedEventPersistence()
    {
        var sink = new CapturingEventSink();
        var host = new RuntimeHost(new FakeAgentLoop(Array.Empty<AgentEvent>()), sink);
        var request = CreateRequest();
        request = new RuntimeRunRequest
        {
            LoopRequest = request.LoopRequest,
            PublishEvents = false
        };

        await CollectAsync(host.RunAsync(request));

        Assert.Empty(sink.Events);
    }

    [Fact]
    public void Runner_RejectsRunContextThatDoesNotMatchLoopRequest()
    {
        var runner = new AgentRuntimeRunner(new RuntimeHost(new FakeAgentLoop(Array.Empty<AgentEvent>())));
        var request = CreateRequest();
        request = new RuntimeRunRequest
        {
            LoopRequest = request.LoopRequest,
            Context = new RunContext { RunId = "another-run", StepId = "step-1" }
        };

        Assert.Throws<InvalidOperationException>(() => runner.RunAsync(request));
    }

    [Fact]
    public async Task RunAsync_RequestCancellation_PersistsCancelledTerminalBeforeRethrowing()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sink = new CapturingEventSink();
        var host = new RuntimeHost(new CancellingAgentLoop(), sink);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CollectAsync(host.RunAsync(CreateRequest(), cancellation.Token)));

        Assert.Equal(RuntimeState.Cancelled, host.State);
        Assert.Equal("run_cancelled", sink.Events[^1].Type);
    }

    [Fact]
    public async Task RunAsync_InternalCancellation_EmitsTimedOutTerminal()
    {
        var sink = new CapturingEventSink();
        var host = new RuntimeHost(new CancellingAgentLoop(), sink);

        var events = await CollectAsync(host.RunAsync(CreateRequest()));

        Assert.Equal(RuntimeState.TimedOut, host.State);
        Assert.Equal("run_timed_out", events[^1].Type);
        Assert.Equal("timed_out", Assert.IsType<AgentResponse>(events[^1].Payload).StopReason);
    }

    private static RuntimeRunRequest CreateRequest()
    {
        return new RuntimeRunRequest
        {
            LoopRequest = new AgentLoopRequest
            {
                RunId = "run-1",
                StepId = "step-1"
            }
        };
    }

    private static async Task<List<RuntimeEvent>> CollectAsync(IAsyncEnumerable<RuntimeEvent> source)
    {
        var result = new List<RuntimeEvent>();
        await foreach (var item in source)
            result.Add(item);
        return result;
    }

    private sealed class FakeAgentLoop(IReadOnlyList<AgentEvent> events) : IAgentLoop
    {
        public async IAsyncEnumerable<AgentEvent> RunAsync(
            AgentLoopRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return item;
            }
        }
    }

    private sealed class CancellingAgentLoop : IAgentLoop
    {
        public async IAsyncEnumerable<AgentEvent> RunAsync(
            AgentLoopRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new OperationCanceledException(cancellationToken);
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class CapturingEventSink : IRuntimeEventSink
    {
        public List<RuntimeEvent> Events { get; } = new();

        public Task PublishAsync(RuntimeEvent runtimeEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(runtimeEvent);
            return Task.CompletedTask;
        }
    }
}
