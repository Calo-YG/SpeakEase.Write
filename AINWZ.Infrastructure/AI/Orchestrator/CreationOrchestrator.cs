using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class CreationOrchestrator
{
    private readonly CreationRouter _router;
    private readonly IOpenAIContext _llmContext;
    private readonly IEnumerable<INovelAgent> _agents;
    private readonly WritingBlackboardBuilder _blackboardBuilder;
    private readonly BlackboardHolder _blackboardHolder;
    private readonly ICreationAgentContext _agentContext;

    public CreationOrchestrator(
        CreationRouter router,
        IOpenAIContext llmContext,
        IEnumerable<INovelAgent> agents,
        WritingBlackboardBuilder blackboardBuilder,
        BlackboardHolder blackboardHolder,
        ICreationAgentContext agentContext)
    {
        _router = router;
        _llmContext = llmContext;
        _agents = agents;
        _blackboardBuilder = blackboardBuilder;
        _blackboardHolder = blackboardHolder;
        _agentContext = agentContext;
    }

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string userMessage,
        List<ChatMessage> conversationHistory = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var route = _router.Decide(userMessage);

        yield return new AgentStreamChunk
        {
            Type = "meta",
            Content = JsonHelper.Serialize(new
            {
                stage = "routing",
                agent = route.AgentName,
                contentType = route.ContentType,
                reason = route.Reason
            })
        };

        yield return new AgentStreamChunk
        {
            Type = "meta",
            Content = JsonHelper.Serialize(new { stage = "loading_context" })
        };

        var blackboard = await _blackboardBuilder.BuildAsync(workId, Guid.NewGuid().ToString());
        _blackboardHolder.Blackboard = blackboard;

        var enrichedMessage = userMessage;
        if (!string.IsNullOrEmpty(workId))
        {
            var ctx = await _agentContext.BuildContext(workId, cancellationToken);
            var contextParts = new List<string>();
            contextParts.Add($"[系统] 当前作品标识 (work_id) = {workId}");
            if (!string.IsNullOrEmpty(ctx.ProjectMemory))
                contextParts.Add(ctx.ProjectMemory);
            enrichedMessage = $"{userMessage}\n\n{string.Join("\n\n", contextParts)}";
        }

        var agent = _agents.FirstOrDefault(a => a.Name == route.AgentName);
        if (agent == null)
        {
            yield return new AgentStreamChunk
            {
                Type = "done",
                Content = JsonHelper.Serialize(new { error = $"未找到 Agent: {route.AgentName}" })
            };
            yield break;
        }

        await _llmContext.ResolveAsync(cancellationToken);

        var request = new AgentRequest
        {
            UserMessage = enrichedMessage,
            SystemPrompt = agent.BuildPrompt(),
            Model = _llmContext.Model,
            MaxIterations = 10,
            ConversationHistory = conversationHistory ?? new List<ChatMessage>()
        };

        await foreach (var chunk in agent.ExecuteStreamAsync(request, cancellationToken))
        {
            yield return chunk;
        }
    }
}
