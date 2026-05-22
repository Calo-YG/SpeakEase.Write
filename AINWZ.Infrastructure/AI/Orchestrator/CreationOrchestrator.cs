using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

// 创作编排器：路由 → 构建上下文 → 串行执行 Agent 管线，以 SSE 流式返回结果
public sealed class CreationOrchestrator(
    CreationRouter router,
    IOpenAIContext llmContext,
    ICreationAgentContext agentContextBuilder,
    IEnumerable<INovelAgent> agents,
    ILogger<CreationOrchestrator> logger)
{
    // 执行完整的 Agent 管线：LLM 意图路由 → 逐个 Agent 执行 → 流式返回 chunk
    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string sessionId,
        string userMessage,
        int maxIterations = 10,
        int? requestedMaxTokens = null,
        double? requestedTemperature = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 步骤1：通过 LLM 进行意图路由，确定管线（pipeline）
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

        // 步骤2：解析 LLM 配置（ApiKey / Model / MaxTokens 等）
        await llmContext.ResolveAsync(cancellationToken);

        // 步骤3：按管线顺序逐个执行 Agent，前一个 Agent 的输出作为后续 Agent 的上下文
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

            // 构建 Agent 专属上下文（历史消息 + 项目记忆 + token 预算裁剪）
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

            // 管线模式下，将前一个 Agent 的结果附带到消息中
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

            // 通过流式枚举器逐块输出 Agent 执行结果
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

            // 任一 Agent 出错则中止整个管线
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

    // 包裹 Agent 的 IAsyncEnumerable，捕获 MoveNextAsync 异常并转为 error chunk
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
                    // 捕获非取消类异常，后续以 error chunk 形式返回
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

    // 解析最大迭代次数，默认 10，范围 [1, 50]
    private static int ResolveMaxIterations(int requested)
    {
        return Math.Clamp(requested <= 0 ? 10 : requested, 1, 50);
    }

    // 解析最大输出 token 数：取 Agent 默认值、请求值、配置值三者中的最小值
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
