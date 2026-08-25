using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging.Abstractions;

using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Infrastructure.AI.Runtime;

namespace AINWZ.Tests.AI;

public sealed class CreationRuntimeFacadeTests
{
    [Fact]
    public async Task ExecuteAsync_ProjectsRunMetadataWithGlobalSequence()
    {
        var orchestrator = new CreationOrchestrator(
            new CreationRouter(NullLogger<CreationRouter>.Instance),
            new TestOpenAIContext(),
            new PipelineRouterLlm(),
            new StaticContextBuilder(),
            new INovelAgent[]
            {
                new RuntimeAgent("first"),
                new RuntimeAgent("second")
            },
            NullLogger<CreationOrchestrator>.Instance);
        var facade = new CreationRuntimeFacade(orchestrator);
        var chunks = new List<AgentStreamChunk>();

        await foreach (var chunk in facade.ExecuteAsync(new AgentRuntimeRequest
        {
            RunId = "run-1",
            WorkId = "work-1",
            SessionId = "session-1",
            UserMessage = "request"
        }))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.Equal("run-1", chunk.RunId));
        Assert.All(chunks, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk.StepId)));
        Assert.Contains(chunks, chunk => chunk.Type == "meta" && chunk.StepId == "runtime");
        Assert.Contains(chunks, chunk => chunk.Type == "meta" && chunk.StepId == "step-1");
        Assert.Contains(chunks, chunk => chunk.Type == "meta" && chunk.StepId == "step-2");
        Assert.Equal(
            Enumerable.Range(1, chunks.Count).Select(x => (long)x),
            chunks.Select(x => x.Sequence));
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

    private sealed class RuntimeAgent(string name) : INovelAgent
    {
        public string Name => name;
        public string DisplayName => name;
        public string RouteDescription => name;
        public AgentMetadata Metadata { get; } = new()
        {
            DefaultParameters = new AgentParameters(0.7, MaxTokens: 2048)
        };

        public string BuildPrompt() => name;

        public void RegisterTools(IToolCapable toolCapable)
        {
        }

        public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
            AgentRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return new AgentStreamChunk
            {
                StepId = name,
                Sequence = 1,
                Type = "content",
                Content = name
            };
            yield return new AgentStreamChunk
            {
                StepId = name,
                Sequence = 2,
                Type = "done",
                FinalResponse = new AgentResponse { Content = name, StopReason = "completed" }
            };
        }
    }
}
