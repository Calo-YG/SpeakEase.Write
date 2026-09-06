using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Agents;
using SpeakEase.Write.Infrastructure.AI.Runtime;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

// 创作编排器：路由 → 构建上下文 → 串行执行 Agent 管线，以 SSE 流式返回结果
public sealed class CreationOrchestrator(
    CreationRouter router,
    IOpenAIContext llmContext,
    IChatCompatible llm,
    ICreationAgentContext agentContextBuilder,
    IEnumerable<INovelAgent> agents,
    ILogger<CreationOrchestrator> logger,
    IAgentRunStore runStore = null,
    PlanCompiler planCompiler = null,
    IAgentRuntimeRunner runtimeRunner = null,
    ArtifactContextBuilder artifactContextBuilder = null,
    PromptCompiler promptCompiler = null,
    ICharacterRuntimeQueue characterRuntimeQueue = null) : IAgentOrchestrator
{
    private readonly PlanCompiler _planCompiler = planCompiler ?? new PlanCompiler();
    private readonly ArtifactContextBuilder _artifactContextBuilder = artifactContextBuilder ?? new ArtifactContextBuilder();
    private readonly PromptCompiler _promptCompiler = promptCompiler ?? new PromptCompiler(new PromptProfileCatalog());
    // 执行完整的 Agent 管线：LLM 意图路由 → 逐个 Agent 执行 → 流式返回 chunk
    public IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string sessionId,
        string userMessage,
        int maxIterations = 10,
        int? requestedMaxTokens = null,
        double? requestedTemperature = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(new AgentRuntimeRequest
        {
            WorkId = workId,
            SessionId = sessionId,
            UserMessage = userMessage,
            MaxIterations = maxIterations,
            MaxTokens = requestedMaxTokens,
            Temperature = requestedTemperature,
            EnableAutoToolDispatch = true
        }, cancellationToken);
    }

    public IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        AgentRuntimeRequest runtimeRequest,
        CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(runtimeRequest, false, false, cancellationToken);

    public IAsyncEnumerable<AgentStreamChunk> ExecuteRuntimeAsync(
        AgentRuntimeRequest runtimeRequest,
        bool enableDynamicToolExposure,
        CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(runtimeRequest, true, enableDynamicToolExposure, cancellationToken);

    private async IAsyncEnumerable<AgentStreamChunk> ExecuteCoreAsync(
        AgentRuntimeRequest runtimeRequest,
        bool useAgentRuntime,
        bool enableDynamicToolExposure,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeRequest);

        var workId = runtimeRequest.WorkId;
        var sessionId = runtimeRequest.SessionId;
        var userMessage = runtimeRequest.UserMessage;
        var maxIterations = runtimeRequest.MaxIterations;
        var requestedMaxTokens = runtimeRequest.MaxTokens;
        var requestedTemperature = runtimeRequest.Temperature;

        // 步骤1：通过 LLM 进行意图路由，确定管线（pipeline）
        var pipelineStopwatch = Stopwatch.StartNew();
        var agentList = agents.ToList();
        var route = await router.DecideWithLLMAsync(userMessage, agentList, llmContext, llm, cancellationToken);
        var requestedPipeline = route.Pipeline.Count > 0 ? route.Pipeline : new List<string> { route.AgentName };
        AgentPlan plan = null;
        string planError = null;
        try
        {
            plan = _planCompiler.Compile(
                new IntentResolution
                {
                    PrimaryAgent = route.AgentName,
                    ExplicitSequence = requestedPipeline,
                    PlanSteps = route.PlanSteps
                },
                agentList.Select(x => x.Name).ToArray());
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Resolved Agent plan is invalid: {Pipeline}", string.Join(" -> ", requestedPipeline));
            planError = ex.Message;
        }

        if (planError is not null)
        {
            yield return new AgentStreamChunk
            {
                Type = "error",
                Content = "AI 执行计划无效。"
            };
            yield return new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = new AgentResponse { StopReason = "invalid_request", Content = string.Empty }
            };
            yield break;
        }

        var pipeline = plan.Steps.Select(x => x.AgentName).ToList();

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


        // 步骤2.5：根据管线 Agent 元数据确定包容性参数，构建一次共享上下文，避免每个 Agent 重复查询数据库
        var pipelineMeta = pipeline
            .Select(name => agentList.FirstOrDefault(a => a.Name == name))
            .Where(a => a is not null)
            .Select(a => a.Metadata)
            .ToList();

        var includeMemory = pipelineMeta.Any(m => m.NeedsProjectMemory);
        var filterHistory = pipelineMeta.All(m => m.ShouldFilterHistory);

        var sharedContext = await agentContextBuilder.BuildContextAsync(
            workId,
            sessionId,
            string.Join("->", pipeline),
            llmContext.Model,
            includeMemory,
            filterHistory,
            llmContext.ContextWindow,
            userMessage,
            cancellationToken);

        if (useAgentRuntime && runtimeRunner is not null)
        {
            var runtimeSteps = plan.Steps.Select(planStep =>
            {
                var agent = agentList.First(x => x.Name == planStep.AgentName);
                if (agent is not AgentBase runtimeAgent)
                    throw new InvalidOperationException($"Agent '{agent.Name}' does not expose a Runtime definition.");
                var meta = agent.Metadata;
                return new RuntimePlanStep
                {
                    Id = planStep.Id,
                    DependsOn = planStep.DependsOn,
                    ContentType = meta.ContentType ?? "plain",
                    CreateRequest = runtimeArtifacts =>
                    {
                        var dependencyArtifacts = planStep.DependsOn
                            .Select(dependencyId => runtimeArtifacts.TryGetValue(dependencyId, out var artifact)
                                ? new AgentArtifact
                                {
                                    Id = $"{artifact.RunId}:{artifact.StepId}",
                                    RunId = artifact.RunId,
                                    StepId = artifact.StepId,
                                    ContentType = artifact.ContentType,
                                    Summary = artifact.Summary,
                                    Content = artifact.Content,
                                    EstimatedTokens = artifact.EstimatedTokens
                                }
                                : null)
                            .Where(artifact => artifact is not null)
                            .ToList();
                        var agentRequest = CreateAgentRequest(
                            runtimeRequest,
                            planStep,
                            agent,
                            meta,
                            sharedContext,
                            dependencyArtifacts,
                            llmContext,
                            requestedMaxTokens,
                            requestedTemperature,
                            maxIterations,
                            userMessage,
                            runStore);
                        return runtimeAgent.CreateRuntimeRequest(
                            agentRequest,
                            enableDynamicToolExposure,
                            cancellationToken);
                    }
                };
            }).ToArray();

            await foreach (var runtimeEvent in runtimeRunner.RunPlanAsync(new RuntimePlanRequest
            {
                Context = new RunContext
                {
                    RunId = runtimeRequest.RunId,
                    UserId = sharedContext.UserId,
                    WorkId = workId,
                    SessionId = sessionId,
                    CancellationToken = cancellationToken
                },
                Steps = runtimeSteps,
                // AgentApplication persists the projected stream with the run's global sequence.
                PublishEvents = false
            }, cancellationToken))
            {
                if (runtimeEvent.Type == "step_completed" && runtimeEvent.Payload is AgentResponse stepResponse)
                {
                    await QueueCharacterRefreshAsync(
                        runtimeRequest,
                        runtimeEvent.StepId,
                        sharedContext.UserId,
                        stepResponse);
                }

                if (runtimeEvent.Chunk is not null)
                {
                    yield return runtimeEvent.Chunk;
                    continue;
                }

                yield return new AgentStreamChunk
                {
                    RunId = runtimeEvent.RunId,
                    StepId = runtimeEvent.StepId,
                    Sequence = runtimeEvent.Sequence,
                    Type = "meta",
                    ContentType = "system",
                    Content = JsonHelper.Serialize(new { stage = runtimeEvent.Type })
                };
            }
            yield break;
        }

        // 步骤4：按计划顺序逐个执行 Agent，并按 DependsOn 注入前置 Artifact
        var artifactsByStep = new Dictionary<string, AgentArtifact>(StringComparer.OrdinalIgnoreCase);
        var executedAgentCount = 0;
        for (var i = 0; i < pipeline.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var planStep = plan.Steps[i];
            var agentName = planStep.AgentName;
            var agent = agentList.FirstOrDefault(a => a.Name == agentName);
            executedAgentCount++;

            var meta = agent.Metadata;
            yield return new AgentStreamChunk
            {
                StepId = planStep.Id,
                Type = "meta",
                Content = JsonHelper.Serialize(new
                {
                    stage = "loading_context",
                    agent = agentName,
                    step = i + 1,
                    total = pipeline.Count
                })
            };

            // 从共享上下文派生 Agent 专属会话历史
            var agentHistory = DeriveAgentConversationHistory(sharedContext.ConversationHistory, meta);

            yield return new AgentStreamChunk
            {
                StepId = planStep.Id,
                Type = "meta",
                Content = JsonHelper.Serialize(new
                {
                    stage = "context_loaded",
                    agent = agentName,
                    snapshotId = string.IsNullOrWhiteSpace(sharedContext.SnapshotId) ? null : sharedContext.SnapshotId,
                    inputTokens = sharedContext.InputTokenCount,
                    trimmed = sharedContext.WasTrimmed || meta.ShouldFilterHistory
                })
            };

            // 仅注入当前 Step 声明的依赖，避免无关 Agent 输出污染上下文。
            var dependencyArtifacts = planStep.DependsOn
                .Select(dependencyId => artifactsByStep.TryGetValue(dependencyId, out var artifact) ? artifact : null)
                .Where(artifact => artifact is not null)
                .ToList();
            var request = CreateAgentRequest(
                runtimeRequest,
                planStep,
                agent,
                meta,
                sharedContext,
                dependencyArtifacts,
                llmContext,
                requestedMaxTokens,
                requestedTemperature,
                maxIterations,
                userMessage,
                runStore);

            // 通过流式枚举器逐块输出 Agent 执行结果
            var stepStopwatch = Stopwatch.StartNew();
            var currentResult = new System.Text.StringBuilder();
            AgentResponse finalResponse = null;
            var hadError = false;

            var agentChunks = useAgentRuntime && runtimeRunner is not null && agent is AgentBase runtimeAgent
                ? runtimeAgent.ExecuteRuntimeStreamAsync(request, runtimeRunner, enableDynamicToolExposure, cancellationToken)
                : agent.ExecuteStreamAsync(request, cancellationToken);
            await foreach (var chunk in StreamAgentChunks(agent.Name, agentChunks, cancellationToken))
            {
                if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                {
                    const int maxChainedResultChars = 12_000;
                    if (currentResult.Length < maxChainedResultChars)
                    {
                        var remaining = maxChainedResultChars - currentResult.Length;
                        currentResult.Append(chunk.Content.AsSpan(0, Math.Min(remaining, chunk.Content.Length)));
                    }
                }

                if (chunk.Type == "done" && chunk.FinalResponse is not null)
                    finalResponse = chunk.FinalResponse;

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

            var artifactContent = !string.IsNullOrWhiteSpace(finalResponse?.Content)
                ? finalResponse.Content
                : currentResult.ToString();
            var artifact = new AgentArtifact
            {
                Id = $"{runtimeRequest.RunId}:{planStep.Id}",
                RunId = runtimeRequest.RunId,
                StepId = planStep.Id,
                ContentType = meta.ContentType ?? "plain",
                Summary = artifactContent.Length > 240 ? artifactContent[..240] : artifactContent,
                Content = artifactContent,
                EstimatedTokens = Math.Max(1, artifactContent.Length / 4)
            };
            artifactsByStep[planStep.Id] = artifact;
            if (runStore is not null && !string.IsNullOrWhiteSpace(runtimeRequest.RunId))
            {
                await runStore.SaveArtifactAsync(
                    runtimeRequest.RunId,
                    planStep.Id,
                    artifact.ContentType,
                    artifact.Summary,
                    artifact.Content,
                    artifact.EstimatedTokens,
                    CancellationToken.None);
            }
            await QueueCharacterRefreshAsync(
                runtimeRequest,
                planStep.Id,
                sharedContext.UserId,
                finalResponse);
        }

        if (executedAgentCount == 0)
        {
            yield return new AgentStreamChunk
            {
                Type = "error",
                Content = "No executable agent was found for the resolved pipeline."
            };
            yield return new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = new AgentResponse
                {
                    Content = string.Empty,
                    StopReason = "invalid_request"
                }
            };
            yield break;
        }

        logger.LogInformation("Pipeline completed, totalElapsed={Elapsed}ms", pipelineStopwatch.ElapsedMilliseconds);
    }

    // 包裹 Agent 的 IAsyncEnumerable，捕获 MoveNextAsync 异常并转为 error chunk
    private async IAsyncEnumerable<AgentStreamChunk> StreamAgentChunks(
        string agentName,
        IAsyncEnumerable<AgentStreamChunk> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var enumerator = chunks.GetAsyncEnumerator(cancellationToken);

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
                    logger.LogError(moveNextException, "Agent [{Agent}] stream failed.", agentName);
                    yield return new AgentStreamChunk
                    {
                        Type = "error",
                        Content = "AI 服务暂时不可用，请稍后重试。"
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

    private async Task QueueCharacterRefreshAsync(
        AgentRuntimeRequest runtimeRequest,
        string stepId,
        string userId,
        AgentResponse response)
    {
        if (characterRuntimeQueue is null || response?.StopReason != "completed")
            return;

        var chapterResult = response.ToolResults?.LastOrDefault(result =>
            result.Success && string.Equals(result.ToolName, "save_chapter_content", StringComparison.Ordinal));
        if (chapterResult?.ExtraData is null ||
            !chapterResult.ExtraData.TryGetValue("chapterId", out var chapterId) ||
            !chapterResult.ExtraData.TryGetValue("content", out var chapterContent) ||
            string.IsNullOrWhiteSpace(chapterContent))
        {
            return;
        }

        await characterRuntimeQueue.EnqueueAsync(new CharacterStateRefreshRequest
        {
            UserId = userId,
            WorkId = runtimeRequest.WorkId,
            SourceRunId = runtimeRequest.RunId,
            SourceChapterId = chapterId,
            SourceArtifactId = $"{runtimeRequest.RunId}:{stepId}",
            ChapterContent = chapterContent
        }, CancellationToken.None);
    }

    private AgentRequest CreateAgentRequest(
        AgentRuntimeRequest runtimeRequest,
        AgentPlanStep planStep,
        INovelAgent agent,
        AgentMetadata meta,
        AgentContext sharedContext,
        IReadOnlyList<AgentArtifact> dependencyArtifacts,
        IOpenAIContext resolvedLlmContext,
        int? requestedMaxTokens,
        double? requestedTemperature,
        int maxIterations,
        string userMessage,
        IAgentRunStore journal)
    {
        var agentHistory = DeriveAgentConversationHistory(sharedContext.ConversationHistory, meta);
        var descriptor = (agent as IAgentDefinition)?.Descriptor;
        var systemPrompt = _promptCompiler.Compile(new PromptCompileRequest
        {
            ProfileKey = descriptor?.PromptProfileKey ?? $"novel.{agent.Name}",
            // 当前请求已作为 L0 UserMessage 传入，避免再复制进 System Prompt 浪费上下文预算。
            TaskObjective = string.Empty,
            Capabilities = descriptor?.ToolGroups ?? Array.Empty<string>(),
            FallbackProfile = agent.BuildPromptProfile()
        });
        var resolvedMaxTokens = ResolveMaxTokens(
            meta.DefaultParameters.MaxTokens,
            requestedMaxTokens,
            resolvedLlmContext.MaxOutputTokens);
        var fixedRequestTokens = EstimateConservativeTokens(agentHistory)
            + EstimateConservativeTokens(systemPrompt)
            + EstimateConservativeTokens(userMessage);
        var dependencyTokenBudget = Math.Max(
            0,
            resolvedLlmContext.ContextWindow - resolvedMaxTokens - fixedRequestTokens - 8);
        var chainMessage = _artifactContextBuilder.Build(
            userMessage,
            dependencyArtifacts,
            dependencyTokenBudget);

        return new AgentRequest
        {
            RunId = runtimeRequest.RunId,
            StepId = planStep.Id,
            UserMessage = chainMessage,
            SystemPrompt = systemPrompt,
            Model = resolvedLlmContext.Model,
            MaxIterations = ResolveMaxIterations(maxIterations),
            Temperature = requestedTemperature ?? meta.DefaultParameters.Temperature,
            TopP = meta.DefaultParameters.TopP,
            FrequencyPenalty = meta.DefaultParameters.FrequencyPenalty,
            PresencePenalty = meta.DefaultParameters.PresencePenalty,
            MaxTokens = resolvedMaxTokens,
            ContextWindowTokens = resolvedLlmContext.ContextWindow,
            ConversationHistory = agentHistory,
            WorkId = runtimeRequest.WorkId,
            SessionId = runtimeRequest.SessionId,
            UserId = sharedContext.UserId,
            SkillName = runtimeRequest.SkillName,
            EnableAutoToolDispatch = runtimeRequest.EnableAutoToolDispatch,
            Journal = journal
        };
    }

    // 这里不追求 tokenizer 精度，而是以 ASCII/中文统一 1.5 token/char 的最坏情况估算。
    private static int EstimateConservativeTokens(IEnumerable<ChatMessage> messages)
    {
        return messages.Sum(message => EstimateConservativeTokens(ExtractMessageText(message)));
    }

    private static int EstimateConservativeTokens(string text)
    {
        return (int)Math.Ceiling((text?.Length ?? 0) * 1.5);
    }

    private static string ExtractMessageText(ChatMessage message)
    {
        return message switch
        {
            SystemMessage system => system.Content ?? string.Empty,
            UserMessage user => user.Content?.ToString() ?? string.Empty,
            AssistantMessage assistant => assistant.Content ?? string.Empty,
            ToolMessage tool => tool.Content ?? string.Empty,
            _ => string.Empty
        };
    }

    // 从共享管线上下文派生 Agent 专属会话历史：
    // - 不需要项目记忆的 Agent 移除 [Session Memory] 系统消息
    // - 需要过滤历史的 Agent 仅保留最近 8 条非系统消息
    private static List<ChatMessage> DeriveAgentConversationHistory(
        List<ChatMessage> sharedHistory,
        AgentMetadata meta)
    {
        var history = new List<ChatMessage>(sharedHistory);

        if (!meta.NeedsProjectMemory)
        {
            history.RemoveAll(m =>
                m is SystemMessage sm &&
                sm.Content?.StartsWith("[Session Memory]", StringComparison.Ordinal) == true);
        }

        if (meta.ShouldFilterHistory)
        {
            var systemMessages = history.Where(m => m is SystemMessage).ToList();
            var nonSystem = history.Where(m => m is not SystemMessage).ToList();
            nonSystem = nonSystem.Count > 8 ? nonSystem.TakeLast(8).ToList() : nonSystem;
            history = systemMessages.Concat(nonSystem).ToList();
        }

        return history;
    }
}
