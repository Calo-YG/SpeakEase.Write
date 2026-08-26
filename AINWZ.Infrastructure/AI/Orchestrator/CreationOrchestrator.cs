using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
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
    PlanCompiler planCompiler = null) : IAgentOrchestrator
{
    private readonly PromptComposer _promptComposer = new();
    private readonly PlanCompiler _planCompiler = planCompiler ?? new PlanCompiler();
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

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        AgentRuntimeRequest runtimeRequest,
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
            cancellationToken);

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
            var systemPrompt = _promptComposer.Compose(agent.BuildPromptProfile());
            var resolvedMaxTokens = ResolveMaxTokens(
                meta.DefaultParameters.MaxTokens,
                requestedMaxTokens,
                llmContext.MaxOutputTokens);
            var fixedRequestTokens = EstimateConservativeTokens(agentHistory)
                + EstimateConservativeTokens(systemPrompt)
                + EstimateConservativeTokens(userMessage);
            var dependencyTokenBudget = Math.Max(
                0,
                llmContext.ContextWindow - resolvedMaxTokens - fixedRequestTokens - 8);
            var chainMessage = BuildDependencyMessage(
                userMessage,
                dependencyArtifacts,
                dependencyTokenBudget);

            var request = new AgentRequest
            {
                RunId = runtimeRequest.RunId,
                StepId = planStep.Id,
                UserMessage = chainMessage,
                SystemPrompt = systemPrompt,
                Model = llmContext.Model,
                MaxIterations = ResolveMaxIterations(maxIterations),
                Temperature = requestedTemperature ?? meta.DefaultParameters.Temperature,
                TopP = meta.DefaultParameters.TopP,
                FrequencyPenalty = meta.DefaultParameters.FrequencyPenalty,
                PresencePenalty = meta.DefaultParameters.PresencePenalty,
                MaxTokens = resolvedMaxTokens,
                ContextWindowTokens = llmContext.ContextWindow,
                ConversationHistory = agentHistory,
                WorkId = workId,
                UserId = sharedContext.UserId,
                SkillName = runtimeRequest.SkillName,
                EnableAutoToolDispatch = runtimeRequest.EnableAutoToolDispatch,
                Journal = runStore
            };

            // 通过流式枚举器逐块输出 Agent 执行结果
            var stepStopwatch = Stopwatch.StartNew();
            var currentResult = new System.Text.StringBuilder();
            AgentResponse finalResponse = null;
            var hadError = false;

            await foreach (var chunk in StreamAgentChunks(agent, request, cancellationToken))
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

    private static string BuildDependencyMessage(
        string userMessage,
        IReadOnlyList<AgentArtifact> dependencies,
        int dependencyTokenBudget)
    {
        if (dependencies.Count == 0)
            return userMessage;

        var aggregateBudget = ResolveDependencyArtifactBudget(dependencyTokenBudget);
        if (aggregateBudget <= 0)
            return userMessage;

        if (dependencies.Count == 1)
        {
            // 预算足够时保持线性管线的历史兼容格式和 12k 单 Artifact 上限。
            const string prefix = "\n\n[Previous agent result]\n";
            var totalBudget = (int)Math.Floor(dependencyTokenBudget / 1.5);
            if (totalBudget <= prefix.Length)
                return userMessage;

            var content = TruncateArtifactContent(
                dependencies[0].Content,
                Math.Min(12_000, totalBudget - prefix.Length));
            return $"{userMessage}{prefix}{content}";
        }

        var metadata = dependencies
            .Select(dependency => new
            {
                Artifact = dependency,
                Prefix = $"\n\n[Dependency artifact: {dependency.StepId}]\nSummary: "
            })
            .ToList();
        var builder = new System.Text.StringBuilder(userMessage);
        var remainingBudget = aggregateBudget;
        var remainingDependencies = metadata.Count;
        foreach (var item in metadata)
        {
            if (remainingBudget < item.Prefix.Length)
            {
                remainingBudget = 0;
                break;
            }

            // 保留每个依赖的标签；摘要按剩余元数据预算平均分配，避免中文等多字节文本撑爆小窗口。
            builder.Append(item.Prefix);
            remainingBudget -= item.Prefix.Length;
            var summaryBudget = Math.Max(0, remainingBudget / remainingDependencies);
            var summary = TruncateArtifactContent(item.Artifact.Summary, summaryBudget);
            builder.Append(summary);
            remainingBudget -= summary.Length;
            remainingDependencies--;
        }

        var remainingContentBudget = Math.Max(0, remainingBudget);
        foreach (var item in metadata)
        {
            if (remainingContentBudget <= 0 || string.IsNullOrWhiteSpace(item.Artifact.Content))
                continue;

            const string contentLabel = "\nContent:\n";
            if (remainingContentBudget <= contentLabel.Length)
                break;

            var content = TruncateArtifactContent(
                item.Artifact.Content,
                remainingContentBudget - contentLabel.Length);
            if (content.Length == 0)
                continue;

            builder.Append(contentLabel).Append(content);
            remainingContentBudget -= contentLabel.Length + content.Length;
        }

        return builder.ToString();
    }

    private static int ResolveDependencyArtifactBudget(int dependencyTokenBudget)
    {
        if (dependencyTokenBudget <= 0)
            return 0;

        // 这是完整请求预算扣除后的 Artifact 子预算，按最坏 1.5 token/char 保守换算。
        return Math.Min(12_000, (int)Math.Floor(dependencyTokenBudget / 1.5));
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

    private static string TruncateArtifactContent(string content, int maxChars)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        return content.Length > maxChars ? content[..maxChars] : content;
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
