using System.Runtime.CompilerServices;
using System.Text.Json;

using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;

namespace AINWZ.Tests.AI;

public sealed class AgentRuntimeEndToEndTests
{
    [Fact]
    public async Task Runner_DirectAnswer_TraversesLifecycleAndReturnsCompatibleDoneChunk()
    {
        var runner = new AgentRuntimeRunner(new RuntimeHost(new AgentLoop()));
        var events = new List<RuntimeEvent>();

        await foreach (var runtimeEvent in runner.RunAsync(new RuntimeRunRequest
        {
            Context = new RunContext { RunId = "run-1", StepId = "step-1", UserId = "user-1", WorkId = "work-1" },
            LoopRequest = new AgentLoopRequest
            {
                RunId = "run-1",
                StepId = "step-1",
                AgentName = "general",
                Llm = new DirectAnswerLlm(),
                Tools = new EmptyTools(),
                Request = new AgentRequest
                {
                    RunId = "run-1",
                    StepId = "step-1",
                    UserId = "user-1",
                    WorkId = "work-1",
                    SystemPrompt = "answer",
                    UserMessage = "hello",
                    Model = "test",
                    MaxIterations = 2,
                    MaxTokens = 128,
                    ContextWindowTokens = 4_096
                }
            }
        }))
        {
            events.Add(runtimeEvent);
        }

        Assert.Equal("run_started", events[0].Type);
        Assert.Equal("run_completed", events[^1].Type);
        var done = Assert.Single(events, x => x.Chunk?.Type == "done");
        Assert.Equal("completed", done.Chunk.FinalResponse.StopReason);
        Assert.Equal("hello back", done.Chunk.FinalResponse.Content);
        Assert.Equal(Enumerable.Range(1, events.Count), events.Select(x => (int)x.Sequence));
    }

    [Fact]
    public void Configuration_KeepsProductionLegacyAndEnablesDevelopmentAgentLoop()
    {
        using var production = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("AINWZ", "appsettings.json")));
        using var development = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("AINWZ", "appsettings.Development.json")));

        Assert.Equal("legacy", production.RootElement.GetProperty("AiRuntime").GetProperty("Mode").GetString());
        Assert.False(production.RootElement.GetProperty("AiRuntime").GetProperty("EnableDynamicToolExposure").GetBoolean());
        Assert.Equal("agent-loop", development.RootElement.GetProperty("AiRuntime").GetProperty("Mode").GetString());
        Assert.True(development.RootElement.GetProperty("AiRuntime").GetProperty("EnableDynamicToolExposure").GetBoolean());
    }

    [Fact]
    public async Task Runner_Plan_SchedulesDependenciesAndPersistsStepState()
    {
        var store = new CapturingRuntimeStateStore();
        var runner = new AgentRuntimeRunner(
            new RuntimeHost(new AgentLoop()),
            new LinearStepScheduler(),
            stateStore: store);
        var events = new List<RuntimeEvent>();
        var plan = new RuntimePlanRequest
        {
            Context = new RunContext { RunId = "run-plan", UserId = "user-1", WorkId = "work-1" },
            PublishEvents = false,
            Steps = new[]
            {
                Step("step-1", Array.Empty<string>(), "first"),
                new RuntimePlanStep
                {
                    Id = "step-2",
                    DependsOn = new[] { "step-1" },
                    ContentType = "plain",
                    CreateRequest = artifacts =>
                    {
                        Assert.Equal("first", artifacts["step-1"].Content);
                        return Request("run-plan", "step-2", "second");
                    }
                }
            }
        };

        await foreach (var runtimeEvent in runner.RunPlanAsync(plan))
            events.Add(runtimeEvent);

        Assert.Equal(new[] { "step-1", "step-2" },
            events.Where(x => x.Type == "step_started").Select(x => x.StepId));
        Assert.Equal(new[] { "first", "second" }, store.Artifacts.Select(x => x.Content));
        Assert.Equal(2, store.Checkpoints.Count(x => x.State == "completed"));
        Assert.Equal(Enumerable.Range(1, events.Count), events.Select(x => (int)x.Sequence));
        Assert.Equal("plan_completed", events[^1].Type);
    }

    [Fact]
    public async Task Runner_Plan_RequestCancellation_PersistsAndPublishesCancelledTerminalState()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new CapturingRuntimeStateStore();
        var sink = new CapturingEventSink();
        var runner = new AgentRuntimeRunner(
            new RuntimeHost(new AgentLoop(), sink),
            new LinearStepScheduler(),
            sink,
            store);
        var plan = new RuntimePlanRequest
        {
            Context = new RunContext { RunId = "run-cancelled", UserId = "user-1", WorkId = "work-1" },
            Steps = new[]
            {
                new RuntimePlanStep
                {
                    Id = "step-cancelled",
                    CreateRequest = _ => Request(
                        "run-cancelled",
                        "step-cancelled",
                        new CallerCancellingLlm(cancellation))
                }
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CollectAsync(runner.RunPlanAsync(plan, cancellation.Token)));

        Assert.Contains(store.Checkpoints, x =>
            x.StepId == "step-cancelled" && x.State == "cancelled");
        Assert.Equal("step_cancelled", sink.Events[^2].Type);
        Assert.Equal("plan_cancelled", sink.Events[^1].Type);
    }

    private static RuntimePlanStep Step(string id, IReadOnlyList<string> dependsOn, string response)
        => new()
        {
            Id = id,
            DependsOn = dependsOn,
            ContentType = "plain",
            CreateRequest = _ => Request("run-plan", id, response)
        };

    private static RuntimeRunRequest Request(string runId, string stepId, string response)
        => Request(runId, stepId, new FixedAnswerLlm(response));

    private static RuntimeRunRequest Request(string runId, string stepId, IChatCompatible llm)
        => new()
        {
            Context = new RunContext { RunId = runId, StepId = stepId, UserId = "user-1", WorkId = "work-1" },
            LoopRequest = new AgentLoopRequest
            {
                RunId = runId,
                StepId = stepId,
                AgentName = stepId,
                Llm = llm,
                Tools = new EmptyTools(),
                Request = new AgentRequest
                {
                    RunId = runId,
                    StepId = stepId,
                    UserId = "user-1",
                    WorkId = "work-1",
                    SystemPrompt = "answer",
                    UserMessage = "hello",
                    Model = "test",
                    MaxIterations = 2,
                    MaxTokens = 128,
                    ContextWindowTokens = 4_096
                }
            }
        };

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }

    private sealed class DirectAnswerLlm : IChatCompatible
    {
        public Task<LLMTurnResult> ChatAsync(LLMTurnContext context, List<ChatMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new LLMTurnChunk { Type = "content", Content = "hello back" };
            yield return new LLMTurnChunk
            {
                Type = "done",
                TurnResult = new LLMTurnResult
                {
                    Success = true,
                    Model = context.Model,
                    Content = "hello back"
                }
            };
        }
    }

    private sealed class FixedAnswerLlm(string answer) : IChatCompatible
    {
        public Task<LLMTurnResult> ChatAsync(LLMTurnContext context, List<ChatMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new LLMTurnChunk { Type = "content", Content = answer };
            yield return new LLMTurnChunk
            {
                Type = "done",
                TurnResult = new LLMTurnResult { Success = true, Model = context.Model, Content = answer }
            };
        }
    }

    private sealed class CallerCancellingLlm(CancellationTokenSource cancellation) : IChatCompatible
    {
        public Task<LLMTurnResult> ChatAsync(LLMTurnContext context, List<ChatMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class CapturingRuntimeStateStore : IRuntimeStateStore
    {
        public List<RuntimeCheckpoint> Checkpoints { get; } = new();
        public List<RuntimeArtifact> Artifacts { get; } = new();

        public Task SaveCheckpointAsync(RuntimeCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            Checkpoints.Add(checkpoint);
            return Task.CompletedTask;
        }

        public Task SaveArtifactAsync(RuntimeArtifact artifact, CancellationToken cancellationToken = default)
        {
            Artifacts.Add(artifact);
            return Task.CompletedTask;
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

    private static async Task<List<RuntimeEvent>> CollectAsync(IAsyncEnumerable<RuntimeEvent> source)
    {
        var events = new List<RuntimeEvent>();
        await foreach (var runtimeEvent in source)
            events.Add(runtimeEvent);
        return events;
    }

    private sealed class EmptyTools : IToolCapable
    {
        public IReadOnlyList<ToolDefinition> Tools { get; } = Array.Empty<ToolDefinition>();
        public void RegisterTool(ToolDefinition tool) => throw new NotSupportedException();
        public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
