using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace AINWZ.Tests.AI;

public sealed class CreationOrchestratorPerformanceTests
{
    [Fact]
    public async Task ExecuteAsync_BoundsPreviousAgentResultBeforeNextPipelineStep()
    {
        var first = new PipelineAgent("first", new string('x', 20_000));
        var second = new PipelineAgent("second", "done");
        var orchestrator = new CreationOrchestrator(
            new CreationRouter(NullLogger<CreationRouter>.Instance),
            new TestOpenAIContext(),
            new PipelineRouterLlm(),
            new StaticContextBuilder(),
            new[] { first, second },
            NullLogger<CreationOrchestrator>.Instance);

        await foreach (var _ in orchestrator.ExecuteAsync("work-1", "session-1", "request"))
        {
        }

        Assert.NotNull(second.CapturedRequest);
        Assert.Equal(12_000, second.CapturedRequest.UserMessage.Length - "request\n\n[Previous agent result]\n".Length);
        Assert.StartsWith("request\n\n[Previous agent result]\n", second.CapturedRequest.UserMessage);
    }

    [Fact]
    public async Task ExecuteAsync_PassesAllDependencyArtifactsToDagStep()
    {
        var first = new PipelineAgent("first", "artifact-from-first");
        var second = new PipelineAgent("second", "artifact-from-second");
        var third = new PipelineAgent("third", "done");
        var orchestrator = new CreationOrchestrator(
            new CreationRouter(NullLogger<CreationRouter>.Instance),
            new TestOpenAIContext(),
            new DagRouterLlm(),
            new StaticContextBuilder(),
            new[] { first, second, third },
            NullLogger<CreationOrchestrator>.Instance);

        await foreach (var _ in orchestrator.ExecuteAsync("work-1", "session-1", "request"))
        {
        }

        Assert.NotNull(third.CapturedRequest);
        Assert.Contains("artifact-from-first", third.CapturedRequest.UserMessage);
        Assert.Contains("artifact-from-second", third.CapturedRequest.UserMessage);
    }

    [Fact]
    public async Task ExecuteAsync_BoundsAggregateDependencyArtifactsAndKeepsEverySummary()
    {
        var first = new PipelineAgent("first", "first-summary|" + new string('a', 20_000));
        var second = new PipelineAgent("second", "second-summary|" + new string('b', 20_000));
        var third = new PipelineAgent("third", "done");
        var orchestrator = new CreationOrchestrator(
            new CreationRouter(NullLogger<CreationRouter>.Instance),
            new TestOpenAIContext(),
            new DagRouterLlm(),
            new StaticContextBuilder(),
            new[] { first, second, third },
            NullLogger<CreationOrchestrator>.Instance);

        await foreach (var _ in orchestrator.ExecuteAsync("work-1", "session-1", "request"))
        {
        }

        Assert.NotNull(third.CapturedRequest);
        Assert.Contains("[Dependency artifact: first-step]", third.CapturedRequest.UserMessage);
        Assert.Contains("Summary: first-summary|", third.CapturedRequest.UserMessage);
        Assert.Contains("[Dependency artifact: second-step]", third.CapturedRequest.UserMessage);
        Assert.Contains("Summary: second-summary|", third.CapturedRequest.UserMessage);
        Assert.True(
            third.CapturedRequest.UserMessage.Length <= 12_200,
            $"Dependency context exceeded aggregate budget: {third.CapturedRequest.UserMessage.Length} chars.");
    }

    private sealed class StaticContextBuilder : ICreationAgentContext
    {
        public Task<AgentContext> BuildContextAsync(
            string workId,
            string sessionId,
            string agentName,
            string primaryModel,
            bool includeMemory,
            bool filterHistory,
            int contextWindowTokens,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentContext { UserId = "user-1" });
        }
    }

    private sealed class PipelineRouterLlm : IChatCompatible
    {
        public Task<LLMTurnResult> ChatAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LLMTurnResult
            {
                Content = "{\"pipeline\":[\"first\",\"second\"],\"reason\":\"test\"}",
                Model = context.Model,
                Success = true
            });
        }

        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class DagRouterLlm : IChatCompatible
    {
        public Task<LLMTurnResult> ChatAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LLMTurnResult
            {
                Content = "{\"agent\":\"third\",\"steps\":[{\"id\":\"first-step\",\"agent\":\"first\",\"dependsOn\":[]},{\"id\":\"second-step\",\"agent\":\"second\",\"dependsOn\":[]},{\"id\":\"third-step\",\"agent\":\"third\",\"dependsOn\":[\"first-step\",\"second-step\"]}],\"reason\":\"test\"}",
                Model = context.Model,
                Success = true
            });
        }

        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class PipelineAgent(string name, string output) : INovelAgent
    {
        public string Name => name;
        public string DisplayName => name;
        public string RouteDescription => name;
        public AgentMetadata Metadata { get; } = new() { DefaultParameters = new AgentParameters(0.7, MaxTokens: 30_000) };
        public AgentRequest CapturedRequest { get; private set; }

        public string BuildPrompt() => name;

        public void RegisterTools(IToolCapable toolCapable)
        {
        }

        public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
            AgentRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            await Task.Yield();
            yield return new AgentStreamChunk { Type = "content", Content = output };
            yield return new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = new AgentResponse { Content = output, StopReason = "completed" }
            };
        }
    }
}
