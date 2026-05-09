using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
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
    IContextCompressor compressor,
    ILogger<CreationOrchestrator> logger)
{
    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string userMessage,
        List<ChatMessage> conversationHistory = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pipelineStopwatch = Stopwatch.StartNew();

        var route = await router.DecideWithLLMAsync(userMessage, cancellationToken);

        logger.LogInformation("路由决策: agent={Agent}, contentType={ContentType}, pipeline={Pipeline}",
            route.AgentName, route.ContentType, string.Join("→", route.Pipeline.Count > 0 ? route.Pipeline : new List<string> { route.AgentName }));

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
        AgentStreamChunk contextErrorChunk = null;
        if (!string.IsNullOrEmpty(workId))
        {
            try
            {
                var ctx = await agentContext.BuildContext(workId, cancellationToken);
                var contextParts = new List<string>();
                contextParts.Add($"[系统] 当前作品标识 (work_id) = {workId}");
                if (!string.IsNullOrEmpty(ctx.ProjectMemory))
                    contextParts.Add(ctx.ProjectMemory);
                enrichedMessage = $"{userMessage}\n\n{string.Join("\n\n", contextParts)}";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "构建上下文失败, workId={WorkId}", workId);
                contextErrorChunk = new AgentStreamChunk
                {
                    Type = "meta",
                    Content = JsonHelper.Serialize(new { stage = "context_error", error = ex.Message })
                };
            }
        }

        if (contextErrorChunk != null)
            yield return contextErrorChunk;

        await llmContext.ResolveAsync(cancellationToken);

        var originalHistoryCount = conversationHistory?.Count ?? 0;
        AgentStreamChunk compressedChunk = null;
        if (conversationHistory is { Count: > 0 })
        {
            try
            {
                conversationHistory = await compressor.CompressAsync(
                    conversationHistory, llmContext.Model, cancellationToken);

                if (conversationHistory.Count < originalHistoryCount)
                {
                    compressedChunk = new AgentStreamChunk
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
            catch (Exception ex)
            {
                logger.LogError(ex, "会话压缩失败，使用原始历史");
            }
        }

        if (compressedChunk != null)
            yield return compressedChunk;

        var pipeline = route.Pipeline.Count > 1 ? route.Pipeline : new List<string> { route.AgentName };

        var previousResult = "";
        for (var i = 0; i < pipeline.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var agentName = pipeline[i];
            var agent = agents.FirstOrDefault(a => a.Name == agentName);
            if (agent == null)
            {
                logger.LogWarning("Pipeline 步骤 {Step}/{Total}: 未找到 Agent [{Agent}]", i + 1, pipeline.Count, agentName);
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
                ConversationHistory = conversationHistory ?? new List<ChatMessage>(),
                WorkId = workId,
            };

            var stepStopwatch = Stopwatch.StartNew();
            previousResult = "";

            var (chunks, error) = await ExecuteAgentStepAsync(agent, request, cancellationToken);

            foreach (var chunk in chunks)
            {
                if (chunk.Type == "content")
                    previousResult += chunk.Content;
                yield return chunk;
            }

            if (error != null)
            {
                logger.LogError(error, "Pipeline 步骤 {Step}/{Total}: Agent [{Agent}] 执行异常, elapsed={Elapsed}ms",
                    i + 1, pipeline.Count, agentName, stepStopwatch.ElapsedMilliseconds);

                yield return new AgentStreamChunk
                {
                    Type = "meta",
                    Content = JsonHelper.Serialize(new
                    {
                        stage = "pipeline_error",
                        agent = agentName,
                        step = i + 1,
                        total = pipeline.Count,
                        error = error.Message
                    })
                };
            }
            else
            {
                logger.LogInformation("Pipeline 步骤 {Step}/{Total}: Agent [{Agent}] 完成, elapsed={Elapsed}ms",
                    i + 1, pipeline.Count, agentName, stepStopwatch.ElapsedMilliseconds);
            }
        }

        logger.LogInformation("Pipeline 全部完成, totalElapsed={Elapsed}ms", pipelineStopwatch.ElapsedMilliseconds);
    }

    private static async Task<(List<AgentStreamChunk> Chunks, Exception Error)> ExecuteAgentStepAsync(
        INovelAgent agent,
        AgentRequest request,
        CancellationToken ct)
    {
        var chunks = new List<AgentStreamChunk>();
        try
        {
            await foreach (var chunk in agent.ExecuteStreamAsync(request, ct))
                chunks.Add(chunk);
            return (chunks, null);
        }
        catch (Exception ex)
        {
            return (chunks, ex);
        }
    }
}
