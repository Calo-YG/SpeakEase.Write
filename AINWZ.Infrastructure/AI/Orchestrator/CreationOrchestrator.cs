using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class CreationOrchestrator
{
    private readonly CreationRouter _router;
    private readonly IEnumerable<INovelAgent> _agents;
    private readonly WritingBlackboardBuilder _blackboardBuilder;
    private readonly BlackboardHolder _blackboardHolder;
    private readonly ICreationAgentContext _agentContext;

    public CreationOrchestrator(
        CreationRouter router,
        IEnumerable<INovelAgent> agents,
        WritingBlackboardBuilder blackboardBuilder,
        BlackboardHolder blackboardHolder,
        ICreationAgentContext agentContext)
    {
        _router = router;
        _agents = agents;
        _blackboardBuilder = blackboardBuilder;
        _blackboardHolder = blackboardHolder;
        _agentContext = agentContext;
    }

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
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
            Content = "{\"stage\":\"loading_context\"}"
        };

        var blackboard = await _blackboardBuilder.BuildAsync(
            workId, Guid.NewGuid().ToString());

        _blackboardHolder.Blackboard = blackboard;

        var enrichedMessage = userMessage;
        if (!string.IsNullOrEmpty(workId))
        {
            var ctx = await _agentContext.BuildContext(workId, cancellationToken);
            if (!string.IsNullOrEmpty(ctx.ProjectMemory))
                enrichedMessage = $"{userMessage}\n\n{ctx.ProjectMemory}";
        }

        var agent = _agents.FirstOrDefault(a => a.Name == route.AgentName);
        if (agent == null)
        {
            yield return new AgentStreamChunk
            {
                Type = "done",
                Content = $"未找到 Agent: {route.AgentName}"
            };
            yield break;
        }

        var prompt = agent.BuildPrompt();

        await foreach (var chunk in agent.ExecuteStreamAsync(
            new AgentRequest
            {
                UserMessage = enrichedMessage,
                SystemPrompt = prompt,
                Model = blackboard.Meta.PreferredModel,
                MaxIterations = 10
            }, cancellationToken))
        {
            yield return chunk;
        }

        yield return new AgentStreamChunk
        {
            Type = "done",
            Content = "generation_complete"
        };
    }
}
