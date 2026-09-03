using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Agents;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Infrastructure.AI.Runtime;

namespace AINWZ.Tests.AI;

public sealed class CreationRuntimeFacadeTests
{
    [Theory]
    [InlineData("legacy")]
    [InlineData("agent-loop")]
    public async Task ExecuteAsync_BothModesKeepSameExternalResult(string mode)
    {
        var llm = new RuntimeModeLlm();
        var tools = new ToolCapable(new ServiceCollection().BuildServiceProvider());
        var agent = new RuntimeBackedAgent(llm, tools);
        var orchestrator = new CreationOrchestrator(
            new CreationRouter(NullLogger<CreationRouter>.Instance),
            new TestOpenAIContext(),
            llm,
            new StaticContextBuilder(),
            new[] { agent },
            NullLogger<CreationOrchestrator>.Instance,
            runtimeRunner: new AgentRuntimeRunner(new RuntimeHost(new AgentLoop())));
        var facade = new CreationRuntimeFacade(orchestrator, Options.Create(new AgentRuntimeModeOptions
        {
            Mode = mode,
            EnableDynamicToolExposure = true
        }));
        var chunks = new List<AgentStreamChunk>();

        await foreach (var chunk in facade.ExecuteAsync(new AgentRuntimeRequest
        {
            RunId = $"run-{mode}", WorkId = "work-1", SessionId = "session-1", UserMessage = "request"
        }))
        {
            chunks.Add(chunk);
        }

        var done = Assert.Single(chunks, x => x.Type == "done");
        Assert.Equal("completed", done.FinalResponse.StopReason);
        Assert.Equal("runtime answer", done.FinalResponse.Content);
        if (mode == "agent-loop")
            Assert.InRange(llm.LastStreamToolCount, 1, 12);
    }

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

    private sealed class RuntimeBackedAgent(IChatCompatible llm, IToolCapable tools)
        : AgentBase(llm, tools, NullLogger<RuntimeBackedAgent>.Instance)
    {
        public override string Name => "write";
        public override string DisplayName => "write";
        public override string BuildPrompt() => "write";

        protected override IEnumerable<ToolDefinition> GetToolDefinitions()
        {
            foreach (var name in new[]
            {
                "get_work_info", "get_outline", "get_recent_chapters", "get_writing_rules",
                "get_character", "get_world_setting", "get_foreshadowing", "get_timeline_events",
                "get_relationships", "save_chapter_content", "update_chapter_summary", "create_timeline_event",
                "search_outline", "list_volumes", "web_search"
            })
            {
                yield return Definition(name);
            }
        }

        private static ToolDefinition Definition(string name)
            => new()
            {
                Function = new FunctionDefinition
                {
                    Name = name,
                    Parameters = new FunctionParameters
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ParameterSchema>()
                    }
                }
            };
    }

    private sealed class RuntimeModeLlm : IChatCompatible
    {
        public int LastStreamToolCount { get; private set; }

        public Task<LLMTurnResult> ChatAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new LLMTurnResult
            {
                Content = "{\"pipeline\":[\"write\"],\"reason\":\"test\"}",
                Model = context.Model,
                Success = true
            });

        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastStreamToolCount = tools.Count;
            await Task.Yield();
            yield return new LLMTurnChunk
            {
                Type = "content",
                Content = "runtime answer"
            };
            yield return new LLMTurnChunk
            {
                Type = "done",
                TurnResult = new LLMTurnResult
                {
                    Content = "runtime answer",
                    Model = context.Model,
                    Success = true
                }
            };
        }
    }
}
