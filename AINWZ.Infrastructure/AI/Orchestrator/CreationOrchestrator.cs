using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class CreationOrchestrator(
    CreationRouter router,
    IOpenAIContext llmContext,
    ICreationAgentContext agentContextBuilder,
    IEnumerable<INovelAgent> agents,
    ILogger<CreationOrchestrator> logger)
{
    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string sessionId,
        string userMessage,
        int maxIterations = 10,
        int? requestedMaxTokens = null,
        double? requestedTemperature = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pipelineStopwatch = Stopwatch.StartNew();
        var agentList = agents.ToList();
        var route = await router.DecideWithLLMAsync(userMessage, agentList, cancellationToken);
        var pipeline = route.Pipeline.Count > 0 ? route.Pipeline : new List<string> { route.AgentName };

        logger.LogInformation(
            "Route decision: agent={Agent}, contentType={ContentType}, pipeline={Pipeline}",
            route.AgentName,
            route.ContentType,
            string.Join(" -> ", pipeline));

        yield return new AgentStreamChunk
        {
            Type = "meta",
            Content = JsonHelper.Serialize(new
            {
                stage = "routing",
                agent = route.AgentName,
                contentType = route.ContentType,
                reason = route.Reason,
                pipeline = pipeline.Count > 1 ? pipeline : null
            })
        };

        await llmContext.ResolveAsync(cancellationToken);

        var previousResult = string.Empty;
        for (var i = 0; i < pipeline.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var agentName = pipeline[i];
            var agent = agentList.FirstOrDefault(a => a.Name == agentName);
            if (agent is null)
            {
                logger.LogWarning("Pipeline step {Step}/{Total}: agent [{Agent}] not found", i + 1, pipeline.Count, agentName);
                yield return new AgentStreamChunk
                {
                    Type = "meta",
                    Content = JsonHelper.Serialize(new { stage = "pipeline_skip", agent = agentName, error = "agent_not_found" })
                };
                continue;
            }

            var meta = agent.Metadata;
            yield return new AgentStreamChunk
            {
                Type = "meta",
                Content = JsonHelper.Serialize(new
                {
                    stage = "loading_context",
                    agent = agentName,
                    step = i + 1,
                    total = pipeline.Count
                })
            };

            var sessionContext = await agentContextBuilder.BuildContextAsync(
                workId,
                sessionId,
                agentName,
                llmContext.Model,
                meta.NeedsProjectMemory,
                meta.ShouldFilterHistory,
                llmContext.ContextWindow,
                cancellationToken);

            yield return new AgentStreamChunk
            {
                Type = "meta",
                Content = JsonHelper.Serialize(new
                {
                    stage = "context_loaded",
                    agent = agentName,
                    snapshotId = string.IsNullOrWhiteSpace(sessionContext.SnapshotId) ? null : sessionContext.SnapshotId,
                    inputTokens = sessionContext.InputTokenCount,
                    trimmed = sessionContext.WasTrimmed
                })
            };

            var chainMessage = i > 0 && !string.IsNullOrEmpty(previousResult)
                ? $"{userMessage}\n\n[Previous agent result]\n{previousResult}"
                : userMessage;

            var request = new AgentRequest
            {
                UserMessage = chainMessage,
                SystemPrompt = agent.BuildPrompt(),
                Model = llmContext.Model,
                MaxIterations = ResolveMaxIterations(maxIterations),
                Temperature = requestedTemperature ?? meta.DefaultParameters.Temperature,
                TopP = meta.DefaultParameters.TopP,
                FrequencyPenalty = meta.DefaultParameters.FrequencyPenalty,
                PresencePenalty = meta.DefaultParameters.PresencePenalty,
                MaxTokens = ResolveMaxTokens(meta.DefaultParameters.MaxTokens, requestedMaxTokens, llmContext.MaxOutputTokens),
                ConversationHistory = sessionContext.ConversationHistory,
                WorkId = workId,
                UserId = sessionContext.UserId
            };

            var stepStopwatch = Stopwatch.StartNew();
            previousResult = string.Empty;
            var hadError = false;

            await foreach (var chunk in StreamAgentChunks(agent, request, cancellationToken))
            {
                if (chunk.Type == "content")
                    previousResult += chunk.Content;

                if (chunk.Type == "error")
                    hadError = true;

                yield return chunk;
            }

            if (hadError)
            {
                logger.LogError(
                    "Pipeline step {Step}/{Total}: agent [{Agent}] failed, elapsed={Elapsed}ms",
                    i + 1,
                    pipeline.Count,
                    agentName,
                    stepStopwatch.ElapsedMilliseconds);
                yield break;
            }
            else
            {
                logger.LogInformation(
                    "Pipeline step {Step}/{Total}: agent [{Agent}] completed, elapsed={Elapsed}ms",
                    i + 1,
                    pipeline.Count,
                    agentName,
                    stepStopwatch.ElapsedMilliseconds);
            }
        }

        logger.LogInformation("Pipeline completed, totalElapsed={Elapsed}ms", pipelineStopwatch.ElapsedMilliseconds);
    }

    private async IAsyncEnumerable<AgentStreamChunk> StreamAgentChunks(
        INovelAgent agent,
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var enumerator = agent.ExecuteStreamAsync(request, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                AgentStreamChunk chunk = null;
                Exception moveNextException = null;
                var hasNext = false;

                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (hasNext)
                        chunk = enumerator.Current;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    moveNextException = ex;
                }

                if (moveNextException is not null)
                {
                    logger.LogError(moveNextException, "Agent [{Agent}] stream failed.", agent.Name);
                    yield return new AgentStreamChunk
                    {
                        Type = "error",
                        Content = moveNextException.Message
                    };
                    yield break;
                }

                if (!hasNext)
                    break;

                yield return chunk;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    private static int ResolveMaxIterations(int requested)
    {
        return Math.Clamp(requested <= 0 ? 10 : requested, 1, 50);
    }

    private static int ResolveMaxTokens(int agentMaxTokens, int? requestedMaxTokens, int configuredMaxTokens)
    {
        var candidates = new List<int>();

        if (agentMaxTokens > 0)
            candidates.Add(agentMaxTokens);

        if (requestedMaxTokens is > 0)
            candidates.Add(requestedMaxTokens.Value);

        if (configuredMaxTokens > 0)
            candidates.Add(configuredMaxTokens);

        return candidates.Count == 0 ? 2048 : candidates.Min();
    }
}
