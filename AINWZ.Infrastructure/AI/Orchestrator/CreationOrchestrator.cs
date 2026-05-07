using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class CreationOrchestrator(
    CreationRouter router,
    IOpenAIContext llmContext,
    IEnumerable<INovelAgent> agents,
    ICreationAgentContext agentContext,
    IContextCompressor compressor)
{
    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string userMessage,
        List<ChatMessage> conversationHistory = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var route = await router.DecideWithLLMAsync(userMessage, cancellationToken);

        yield return new AgentStreamChunk
        {
            Type = "meta",
            Content = JsonHelper.Serialize(new
            {
                stage = "routing",
                agent = route.AgentName,
                contentType = route.ContentType,
                reason = route.Reason,
                pipeline = route.Pipeline.Count > 0 ? route.Pipeline : null
            })
        };

        yield return new AgentStreamChunk
        {
            Type = "meta",
            Content = JsonHelper.Serialize(new { stage = "loading_context" })
        };

        var enrichedMessage = userMessage;
        if (!string.IsNullOrEmpty(workId))
        {
            var ctx = await agentContext.BuildContext(workId, cancellationToken);
            var contextParts = new List<string>();
            contextParts.Add($"[系统] 当前作品标识 (work_id) = {workId}");
            if (!string.IsNullOrEmpty(ctx.ProjectMemory))
                contextParts.Add(ctx.ProjectMemory);
            enrichedMessage = $"{userMessage}\n\n{string.Join("\n\n", contextParts)}";
        }

        await llmContext.ResolveAsync(cancellationToken);

        var originalHistoryCount = conversationHistory?.Count ?? 0;
        if (conversationHistory is { Count: > 0 })
        {
            conversationHistory = await compressor.CompressAsync(
                conversationHistory, llmContext.Model, cancellationToken);

            if (conversationHistory.Count < originalHistoryCount)
            {
                yield return new AgentStreamChunk
                {
                    Type = "meta",
                    Content = JsonHelper.Serialize(new
                    {
                        stage = "context_compressed",
                        originalCount = originalHistoryCount,
                        compressedCount = conversationHistory.Count
                    })
                };
            }
        }

        var pipeline = route.Pipeline.Count > 1 ? route.Pipeline : new List<string> { route.AgentName };

        var previousResult = "";
        for (var i = 0; i < pipeline.Count; i++)
        {
            var agentName = pipeline[i];
            var agent = agents.FirstOrDefault(a => a.Name == agentName);
            if (agent == null)
            {
                yield return new AgentStreamChunk
                {
                    Type = "meta",
                    Content = JsonHelper.Serialize(new { stage = "pipeline_skip", agent = agentName, error = "未找到该Agent" })
                };
                continue;
            }

            if (i > 0)
            {
                yield return new AgentStreamChunk
                {
                    Type = "meta",
                    Content = JsonHelper.Serialize(new { stage = "pipeline_next", agent = agentName, step = i + 1, total = pipeline.Count })
                };
            }

            var chainMessage = i > 0 && !string.IsNullOrEmpty(previousResult)
                ? $"{enrichedMessage}\n\n[前一步Agent结果]\n{previousResult}"
                : enrichedMessage;

            var request = new AgentRequest
            {
                UserMessage = chainMessage,
                SystemPrompt = agent.BuildPrompt(),
                Model = llmContext.Model,
                MaxIterations = 10,
                ConversationHistory = conversationHistory ?? new List<ChatMessage>()
            };

            previousResult = "";
            await foreach (var chunk in agent.ExecuteStreamAsync(request, cancellationToken))
            {
                if (chunk.Type == "content")
                    previousResult += chunk.Content;
                yield return chunk;
            }
        }
    }
}
