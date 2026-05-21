using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class CreationOrchestrator(
    CreationRouter router,
    IOpenAIContext llmContext,
    IEnumerable<INovelAgent> agents,
    ILogger<CreationOrchestrator> logger)
{
    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string userMessage,
        List<ChatMessage> conversationHistory = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pipelineStopwatch = Stopwatch.StartNew();

        var route = await router.DecideWithLLMAsync(userMessage, agents, cancellationToken);

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

        var firstAgent = agents.FirstOrDefault(a => a.Name == route.AgentName);

        await llmContext.ResolveAsync(cancellationToken);

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

            var agentHistory = conversationHistory ?? new List<ChatMessage>();

            var meta = agent.Metadata;
            var request = new AgentRequest
            {
                UserMessage = chainMessage,
                SystemPrompt = agent.BuildPrompt(),
                Model = llmContext.Model,
                MaxIterations = 10,
                Temperature = meta.DefaultParameters.Temperature,
                TopP = meta.DefaultParameters.TopP,
                FrequencyPenalty = meta.DefaultParameters.FrequencyPenalty,
                PresencePenalty = meta.DefaultParameters.PresencePenalty,
                MaxTokens = meta.DefaultParameters.MaxTokens,
                ConversationHistory = agentHistory,
                WorkId = workId,
            };

            var stepStopwatch = Stopwatch.StartNew();
            previousResult = "";

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
                logger.LogError("Pipeline 步骤 {Step}/{Total}: Agent [{Agent}] 执行异常, elapsed={Elapsed}ms",
                    i + 1, pipeline.Count, agentName, stepStopwatch.ElapsedMilliseconds);
            }
            else
            {
                logger.LogInformation("Pipeline 步骤 {Step}/{Total}: Agent [{Agent}] 完成, elapsed={Elapsed}ms",
                    i + 1, pipeline.Count, agentName, stepStopwatch.ElapsedMilliseconds);
            }
        }

        logger.LogInformation("Pipeline 全部完成, totalElapsed={Elapsed}ms", pipelineStopwatch.ElapsedMilliseconds);
    }

    private async IAsyncEnumerable<AgentStreamChunk> StreamAgentChunks(
        INovelAgent agent,
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<AgentStreamChunk>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true
        });

        var agentTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in agent.ExecuteStreamAsync(request, ct))
                {
                    if (!channel.Writer.TryWrite(chunk))
                        await channel.Writer.WriteAsync(chunk, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Orchestrator] Agent [{Agent}] 执行异常", agent.Name);
                try
                {
                    channel.Writer.TryWrite(new AgentStreamChunk
                    {
                        Type = "error",
                        Content = $"Agent 执行异常: {ex.Message}"
                    });
                }
                catch { }
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        });

        await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
        {
            yield return chunk;
        }

        try { await agentTask; }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogError(ex, "Agent task faulted"); }
    }
}
