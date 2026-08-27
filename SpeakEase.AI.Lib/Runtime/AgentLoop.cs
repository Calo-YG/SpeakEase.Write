using System.Runtime.CompilerServices;
using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// 通用 Agent 执行循环：模型单轮交互、Tool 调用、消息回填和终止状态由此处统一管理。
/// 该类不规定 ReAct/Thought 协议，也不依赖具体业务 Agent。
/// </summary>
public sealed class AgentLoop : IAgentLoop
{
    private static readonly TimeSpan MaximumToolJournalCompletionTimeout =
        TimeSpan.FromMilliseconds(uint.MaxValue - 1d);

    public async IAsyncEnumerable<AgentStreamChunk> RunAsync(
        AgentLoopRequest loopRequest,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loopRequest);
        ArgumentNullException.ThrowIfNull(loopRequest.Request);
        ArgumentNullException.ThrowIfNull(loopRequest.Llm);
        ArgumentNullException.ThrowIfNull(loopRequest.Tools);

        var request = loopRequest.Request;
        var options = loopRequest.Options ?? new AgentLoopOptions();
        if (loopRequest.Journal is not null &&
            (options.ToolJournalCompletionTimeout <= TimeSpan.Zero ||
             options.ToolJournalCompletionTimeout > MaximumToolJournalCompletionTimeout))
        {
            throw new ArgumentOutOfRangeException(
                nameof(AgentLoopOptions.ToolJournalCompletionTimeout),
                options.ToolJournalCompletionTimeout,
                $"Tool journal completion timeout must be greater than zero and no greater than {MaximumToolJournalCompletionTimeout}.");
        }
        var maxIterations = Math.Clamp(
            request.MaxIterations > 0 ? request.MaxIterations : options.MaxIterations,
            1,
            50);
        var maxToolCalls = Math.Clamp(options.MaxToolCalls <= 0 ? 30 : options.MaxToolCalls, 1, 100);
        using var timeoutCts = options.RunTimeout > TimeSpan.Zero
            ? new CancellationTokenSource(options.RunTimeout)
            : null;
        using var linkedCts = timeoutCts is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var runtimeToken = linkedCts?.Token ?? cancellationToken;
        var messages = BuildMessages(request);
        var historyGroups = BuildHistoryGroups(request.ConversationHistory);
        SystemMessage resolvedSkillMessage = null;
        if (loopRequest.SkillResolver is not null && !string.IsNullOrWhiteSpace(request.SkillName))
        {
            var skill = await loopRequest.SkillResolver.ResolveAsync(request.SkillName, runtimeToken);
            if (!string.IsNullOrWhiteSpace(skill.Content))
            {
                resolvedSkillMessage = ChatMessage.System($"[Resolved Skill: {skill.SkillName}]\n{skill.Content}");
                messages.Insert(0, resolvedSkillMessage);
            }
        }
        var llmContext = new LLMTurnContext
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            TopP = request.TopP,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty
        };
        var totalUsage = new UsageInfo();
        var toolResults = new List<ToolResult>();
        long sequence = 0;

        var executedToolCalls = 0;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            runtimeToken.ThrowIfCancellationRequested();

            LLMTurnResult turnResult = null;
            var exposedTools = request.EnableAutoToolDispatch ? loopRequest.Tools.Tools : Array.Empty<ToolDefinition>();
            var contextWindowTokens = request.ContextWindowTokens > 0
                ? request.ContextWindowTokens
                : options.ContextWindowTokens;
            var reservedOutputTokens = request.MaxTokens is > 0
                ? request.MaxTokens.Value
                : options.MaxOutputTokens;
            if (!TryFitRequestToBudget(
                    messages,
                    exposedTools,
                    contextWindowTokens,
                    reservedOutputTokens,
                    Math.Max(0, options.ImageContentTokenBudget),
                    historyGroups,
                    ref resolvedSkillMessage))
            {
                yield return Mark(request, ref sequence, new AgentStreamChunk
                {
                    Type = "error",
                    Content = "The complete agent request exceeds the model context budget."
                });
                yield return Mark(request, ref sequence, new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = new AgentResponse
                    {
                        Content = string.Empty,
                        Model = request.Model,
                        Iterations = iteration,
                        StopReason = "context_budget_exceeded",
                        ConversationHistory = new List<ChatMessage>(messages),
                        ToolResults = new List<ToolResult>(toolResults),
                        TotalUsage = totalUsage
                    }
                });
                yield break;
            }

            await foreach (var turnChunk in loopRequest.Llm.StreamAsync(
                llmContext,
                messages,
                exposedTools,
                runtimeToken))
            {
                switch (turnChunk.Type)
                {
                    case "content":
                        yield return Mark(request, ref sequence, new AgentStreamChunk { Type = "content", Content = turnChunk.Content });
                        break;
                    case "reasoning":
                        yield return Mark(request, ref sequence, new AgentStreamChunk { Type = "reasoning", Content = turnChunk.Content });
                        break;
                    case "tool_call":
                        yield return Mark(request, ref sequence, new AgentStreamChunk
                        {
                            Type = "tool_call",
                            ToolCallDelta = turnChunk.ToolCallDelta
                        });
                        break;
                    case "done":
                        turnResult = turnChunk.TurnResult;
                        break;
                }
            }

            if (turnResult is null)
                continue;

            AccumulateUsage(totalUsage, turnResult.Usage);

            if (!turnResult.Success)
            {
                yield return Mark(request, ref sequence, new AgentStreamChunk
                {
                    Type = "error",
                    Content = string.IsNullOrWhiteSpace(turnResult.ErrorMessage)
                        ? "LLM call failed."
                        : turnResult.ErrorMessage
                });
                yield return Mark(request, ref sequence, new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = CreateResponse(
                        request,
                        turnResult,
                        iteration + 1,
                        "llm_error",
                        messages,
                        toolResults,
                        totalUsage)
                });
                yield break;
            }

            if (turnResult.HasToolCalls && request.EnableAutoToolDispatch)
            {
                messages.Add(new AssistantMessage
                {
                    Content = turnResult.Content ?? string.Empty,
                    ReasoningContent = turnResult.ReasoningContent,
                    ToolCalls = turnResult.ToolCalls
                });

                for (var toolCallIndex = 0; toolCallIndex < turnResult.ToolCalls.Count; toolCallIndex++)
                {
                    var toolCall = turnResult.ToolCalls[toolCallIndex];
                    var toolExecutionKey = $"{iteration}:{toolCallIndex}";
                    yield return Mark(request, ref sequence, new AgentStreamChunk
                    {
                        Type = "tool_call",
                        ToolCall = toolCall
                    });
                    executedToolCalls++;
                    if (executedToolCalls > maxToolCalls)
                    {
                        yield return Mark(request, ref sequence, new AgentStreamChunk
                        {
                            Type = "done",
                            FinalResponse = CreateResponse(
                                request,
                                turnResult,
                                iteration + 1,
                                "max_tool_calls_reached",
                                messages,
                                toolResults,
                                totalUsage)
                        });
                        yield break;
                    }

                    ToolResult toolResult;
                    var lease = loopRequest.Journal is null
                        ? ToolExecutionLease.Execute()
                        : await loopRequest.Journal.BeginAsync(
                            request.RunId,
                            request.StepId,
                            toolExecutionKey,
                            toolCall,
                            runtimeToken);

                    if (!lease.ShouldExecute && lease.ReplayResult is not null)
                    {
                        toolResult = lease.ReplayResult;
                    }
                    else
                    {
                        try
                        {
                            toolResult = await loopRequest.Tools.ExecuteAsync(toolCall, runtimeToken);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            toolResult = ToolResult.Fail("Tool execution failed.", "tool_execution_error");
                            toolResult.ToolCallId = toolCall.Id;
                            toolResult.ToolName = toolCall.Function?.Name;
                        }

                        if (loopRequest.Journal is not null)
                        {
                            // Tool side effects are complete, so journal persistence gets an independent
                            // bounded window instead of inheriting client or run cancellation.
                            using var journalCompletionCts = new CancellationTokenSource(
                                options.ToolJournalCompletionTimeout);
                            await loopRequest.Journal.CompleteAsync(
                                request.RunId,
                                request.StepId,
                                toolExecutionKey,
                                toolCall,
                                toolResult,
                                journalCompletionCts.Token);
                        }
                    }

                    runtimeToken.ThrowIfCancellationRequested();
                    toolResults.Add(toolResult);
                    yield return Mark(request, ref sequence, new AgentStreamChunk
                    {
                        Type = "tool_result",
                        ToolResult = toolResult
                    });
                    messages.Add(ChatMessage.Tool(toolCall.Id, toolResult.Content ?? string.Empty));
                }

                continue;
            }

            if (turnResult.HasToolCalls && !request.EnableAutoToolDispatch)
            {
                yield return Mark(request, ref sequence, new AgentStreamChunk
                {
                    Type = "error",
                    Content = "Tool dispatch is disabled for this run."
                });
                yield return Mark(request, ref sequence, new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = CreateResponse(
                        request,
                        turnResult,
                        iteration + 1,
                        "tool_dispatch_disabled",
                        messages,
                        toolResults,
                        totalUsage)
                });
                yield break;
            }

            messages.Add(ChatMessage.Assistant(turnResult.Content, turnResult.ReasoningContent));
            yield return Mark(request, ref sequence, new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = CreateResponse(
                    request,
                    turnResult,
                    iteration + 1,
                    "completed",
                    messages,
                    toolResults,
                    totalUsage)
            });
            yield break;
        }

        yield return Mark(request, ref sequence, new AgentStreamChunk
        {
            Type = "done",
            FinalResponse = new AgentResponse
            {
                Content = string.Empty,
                Model = request.Model,
                Iterations = maxIterations,
                StopReason = timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested
                    ? "timed_out"
                    : "max_iterations_reached",
                ConversationHistory = messages,
                ToolResults = toolResults,
                TotalUsage = totalUsage
            }
        });
    }

    async IAsyncEnumerable<AgentEvent> IAgentLoop.RunAsync(
        AgentLoopRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long sequence = 0;
        await foreach (var chunk in RunAsync(request, cancellationToken))
        {
            chunk.RunId = request.RunId;
            chunk.StepId = request.StepId;
            chunk.Sequence = ++sequence;
            yield return new AgentEvent
            {
                RunId = request.RunId,
                StepId = request.StepId,
                Sequence = sequence,
                Type = chunk.Type ?? string.Empty,
                Payload = chunk
            };
        }
    }

    private static List<ChatMessage> BuildMessages(AgentRequest request)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(ChatMessage.System(request.SystemPrompt));

        if (request.ConversationHistory is { Count: > 0 })
            messages.AddRange(request.ConversationHistory);

        if (!string.IsNullOrWhiteSpace(request.UserMessage))
            messages.Add(ChatMessage.User(request.UserMessage));

        return messages;
    }

    private static List<List<ChatMessage>> BuildHistoryGroups(IReadOnlyList<ChatMessage> history)
    {
        var groups = new List<List<ChatMessage>>();
        if (history is null)
            return groups;

        List<ChatMessage> current = null;
        foreach (var message in history)
        {
            if (message is UserMessage || current is null)
            {
                current = new List<ChatMessage>();
                groups.Add(current);
            }

            current.Add(message);
        }

        return groups;
    }

    private static bool TryFitRequestToBudget(
        List<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        int contextWindowTokens,
        int reservedOutputTokens,
        int imageContentTokenBudget,
        List<List<ChatMessage>> historyGroups,
        ref SystemMessage resolvedSkillMessage)
    {
        if (contextWindowTokens <= 0 || reservedOutputTokens < 0 || reservedOutputTokens >= contextWindowTokens)
            return false;

        var inputBudget = contextWindowTokens - reservedOutputTokens;
        while (EstimateRequestTokens(messages, tools, imageContentTokenBudget) > inputBudget && historyGroups.Count > 0)
        {
            var oldestTurn = historyGroups[0];
            historyGroups.RemoveAt(0);
            foreach (var message in oldestTurn)
                messages.Remove(message);
        }

        if (EstimateRequestTokens(messages, tools, imageContentTokenBudget) > inputBudget && resolvedSkillMessage is not null)
        {
            messages.Remove(resolvedSkillMessage);
            resolvedSkillMessage = null;
        }

        return EstimateRequestTokens(messages, tools, imageContentTokenBudget) <= inputBudget;
    }

    private static int EstimateRequestTokens(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        int imageContentTokenBudget)
    {
        var total = messages.Sum(message => EstimateMessageTokens(message, imageContentTokenBudget));
        if (tools is { Count: > 0 })
            total += 4 + JsonSerializer.SerializeToUtf8Bytes(tools).Length;
        return total;
    }

    private static int EstimateMessageTokens(ChatMessage message, int imageContentTokenBudget)
    {
        const int messageFramingTokens = 4;
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(message, typeof(ChatMessage)).Length;
        var imageCount = message is UserMessage { Content: IEnumerable<ContentPart> parts }
            ? parts.Count(part => part.ImageUrl is not null ||
                                  string.Equals(part.Type, "image_url", StringComparison.OrdinalIgnoreCase))
            : 0;
        return messageFramingTokens + payloadBytes + imageCount * imageContentTokenBudget;
    }

    private static AgentStreamChunk Mark(AgentRequest request, ref long sequence, AgentStreamChunk chunk)
    {
        chunk.RunId = request.RunId ?? string.Empty;
        chunk.StepId = request.StepId ?? string.Empty;
        chunk.Sequence = ++sequence;
        return chunk;
    }

    private static AgentResponse CreateResponse(
        AgentRequest request,
        LLMTurnResult turnResult,
        int iterations,
        string stopReason,
        List<ChatMessage> messages,
        List<ToolResult> toolResults,
        UsageInfo totalUsage)
    {
        return new AgentResponse
        {
            Content = stopReason == "completed" ? turnResult.Content : string.Empty,
            ReasoningContent = turnResult.ReasoningContent,
            Model = turnResult.Model ?? request.Model,
            ToolResults = new List<ToolResult>(toolResults),
            ConversationHistory = new List<ChatMessage>(messages),
            Iterations = iterations,
            StopReason = stopReason,
            TotalUsage = totalUsage
        };
    }

    private static void AccumulateUsage(UsageInfo total, UsageInfo increment)
    {
        if (increment is null)
            return;

        total.PromptTokens += increment.PromptTokens;
        total.CompletionTokens += increment.CompletionTokens;
        total.TotalTokens += increment.TotalTokens;
    }
}
