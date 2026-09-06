using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace AINWZ.Tests.AI;

public sealed class IntentResolverTests
{
    [Fact]
    public async Task ResolveAsync_BuildsAgentRegistryWithoutHardCodedKeywordRules()
    {
        var llm = new CapturingIntentLlm("""{"agent":"alpha","confidence":0.88,"goals":["review"],"reason":"best fit"}""");
        var agents = new INovelAgent[]
        {
            new IntentAgent("alpha", "分析文本质量"),
            new IntentAgent("beta", "创作新文本")
        };

        var result = await new IntentResolver().ResolveAsync(
            "请处理这段内容",
            agents,
            new TestOpenAIContext(),
            llm);

        Assert.Equal("alpha", result.PrimaryAgent);
        Assert.Equal(0.88, result.Confidence);
        Assert.Equal(new[] { "review" }, result.Goals);
        Assert.Contains("alpha: 分析文本质量", llm.SystemPrompt);
        Assert.Contains("beta: 创作新文本", llm.SystemPrompt);
        Assert.DoesNotContain("典型场景", llm.SystemPrompt);
        Assert.DoesNotContain("关键词", llm.SystemPrompt);
    }

    [Fact]
    public async Task ResolveAsync_ParsesExplicitPlanStepsForCompiler()
    {
        var llm = new CapturingIntentLlm(
            """{"agent":"write","steps":[{"id":"review","agent":"critique","dependsOn":["write"]},{"id":"write","agent":"write","dependsOn":[]}] }""");

        var result = await new IntentResolver().ResolveAsync(
            "先写再审查",
            new INovelAgent[] { new IntentAgent("write", "写作"), new IntentAgent("critique", "审查") },
            new TestOpenAIContext(),
            llm);

        Assert.Equal(new[] { "review", "write" }, result.PlanSteps.Select(x => x.Id));
        Assert.Equal(new[] { "write" }, result.PlanSteps[0].DependsOn);
    }

    private sealed class CapturingIntentLlm(string response) : IChatCompatible
    {
        public string SystemPrompt { get; private set; } = string.Empty;

        public Task<LLMTurnResult> ChatAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            SystemPrompt = Assert.IsType<SystemMessage>(messages[0]).Content;
            return Task.FromResult(new LLMTurnResult { Success = true, Content = response });
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

    private sealed class IntentAgent(string name, string description) : INovelAgent
    {
        public string Name => name;
        public string DisplayName => name;
        public string RouteDescription => description;
        public AgentMetadata Metadata { get; } = new();
        public string BuildPrompt() => name;
        public void RegisterTools(IToolCapable toolCapable) { }
        public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
            AgentRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
