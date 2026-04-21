namespace SpeakEase.AI.Lib;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using OAI = SpeakEase.AI.Lib.OpenAIModel;

/// <summary>
/// ReAct 模式 Agent 实现，支持 Tool 注册、Skill 注册、Pipeline Filter 和流式/非流式执行。
/// </summary>
public sealed class ReActAgent : IReActAgent
{
    private readonly IChatCompatible _chatCompatible;
    private readonly Dictionary<string, IToolExecutor> _tools = new();
    private readonly Dictionary<string, SkillDefinition> _skills = new();
    private readonly List<IAgentPipelineFilter> _filters = new();

    public ReActAgent(IChatCompatible chatCompatible)
    {
        _chatCompatible = chatCompatible ?? throw new ArgumentNullException(nameof(chatCompatible));
    }

    /// <inheritdoc />
    public void RegisterTool(IToolExecutor tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools[tool.ToolDefinition.Function.Name] = tool;
    }

    /// <inheritdoc />
    public void RegisterSkill(SkillDefinition skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _skills[skill.Name] = skill;
    }

    /// <inheritdoc />
    public void UsePipelineFilter(IAgentPipelineFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _filters.Add(filter);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. 构建初始消息列表
        var messages = BuildInitialMessages(request);

        // 2. 转换工具定义
        var oaiTools = BuildOaiTools();

        // 累积 usage
        var totalUsage = new OAI.UsageInfo();
        var allToolResults = new List<ToolResult>();
        int iteration = 0;

        // 3. ReAct 循环
        for (; iteration < request.MaxIterations; iteration++)
        {
            var oaiRequest = new OAI.ChatCompletionRequest
            {
                Model = request.Model,
                Messages = new List<OAI.ChatMessage>(messages),
                Tools = oaiTools.Count > 0 ? oaiTools : null,
                ToolChoice = oaiTools.Count > 0 ? OAI.ToolChoice.Auto : null,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens
            };

            var pipelineContext = new AgentPipelineContext
            {
                CurrentIteration = iteration,
                MaxIterations = request.MaxIterations,
                ExecutedToolResults = allToolResults
            };

            var pipeline = BuildPipeline(pipelineContext, cancellationToken);
            var response = await pipeline(oaiRequest);

            // 累加 usage
            if (response.Usage != null)
            {
                totalUsage.PromptTokens += response.Usage.PromptTokens;
                totalUsage.CompletionTokens += response.Usage.CompletionTokens;
                totalUsage.TotalTokens += response.Usage.TotalTokens;
            }

            var firstChoice = response.Choices?.FirstOrDefault();
            if (firstChoice == null)
                break;

            // 4. 检查 FinishReason
            var hasToolCalls = firstChoice.Message?.ToolCalls?.Any() ?? false;

            if (hasToolCalls)
            {
                // 追加 AssistantMessage（含 tool_calls）
                messages.Add(new OAI.AssistantMessage
                {
                    Content = firstChoice.Message?.Content,
                    ToolCalls = firstChoice.Message!.ToolCalls
                });

                // 执行每个工具调用并追加 ToolMessage
                foreach (var toolCall in firstChoice.Message.ToolCalls!)
                {
                    var toolResult = await ExecuteToolCallAsync(toolCall, cancellationToken);
                    allToolResults.Add(toolResult);

                    messages.Add(OAI.ChatMessage.Tool(toolCall.Id, toolResult.Content ?? string.Empty));
                }
            }
            else
            {
                // 无 tool_calls，循环结束
                var finalContent = firstChoice.Message?.Content ?? string.Empty;
                messages.Add(OAI.ChatMessage.Assistant(finalContent));

                return new AgentResponse
                {
                    Content = finalContent,
                    Model = response.Model,
                    ToolResults = allToolResults,
                    ConversationHistory = messages,
                    Iterations = iteration + 1,
                    StopReason = "completed",
                    TotalUsage = totalUsage
                };
            }
        }

        // 超出最大迭代次数
        return new AgentResponse
        {
            Content = string.Empty,
            Model = request.Model,
            ToolResults = allToolResults,
            ConversationHistory = messages,
            Iterations = iteration,
            StopReason = "max_iterations_reached",
            TotalUsage = totalUsage
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messages = BuildInitialMessages(request);
        var oaiTools = BuildOaiTools();
        var totalUsage = new OAI.UsageInfo();
        var allToolResults = new List<ToolResult>();
        int iteration = 0;

        for (; iteration < request.MaxIterations; iteration++)
        {
            var oaiRequest = new OAI.ChatCompletionRequest
            {
                Model = request.Model,
                Messages = new List<OAI.ChatMessage>(messages),
                Tools = oaiTools.Count > 0 ? oaiTools : null,
                ToolChoice = oaiTools.Count > 0 ? OAI.ToolChoice.Auto : null,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Stream = true
            };

            var pipelineContext = new AgentPipelineContext
            {
                CurrentIteration = iteration,
                MaxIterations = request.MaxIterations,
                ExecutedToolResults = allToolResults
            };

            // 累积流式内容
            var contentBuilder = new System.Text.StringBuilder();
            var toolCallAccumulators = new Dictionary<int, OAI.ToolCallAccumulator>();
            string finishReason = null;
            string responseModel = request.Model;

            await foreach (var chunk in _chatCompatible.StreamAsync(oaiRequest, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Model))
                    responseModel = chunk.Model;

                if (chunk.Usage != null)
                {
                    totalUsage.PromptTokens += chunk.Usage.PromptTokens;
                    totalUsage.CompletionTokens += chunk.Usage.CompletionTokens;
                    totalUsage.TotalTokens += chunk.Usage.TotalTokens;
                }

                var choice = chunk.Choices?.FirstOrDefault();
                if (choice == null)
                    continue;

                if (!string.IsNullOrEmpty(choice.FinishReason))
                    finishReason = choice.FinishReason;

                var delta = choice.Delta;
                if (delta == null)
                    continue;

                // 内容增量
                if (!string.IsNullOrEmpty(delta.Content))
                {
                    contentBuilder.Append(delta.Content);
                    yield return new AgentStreamChunk
                    {
                        Type = "content",
                        Content = delta.Content
                    };
                }

                // 工具调用增量
                if (delta.ToolCalls != null)
                {
                    foreach (var toolCallDelta in delta.ToolCalls)
                    {
                        OAI.StreamToolCallHelper.MergeDelta(toolCallAccumulators, toolCallDelta);

                        yield return new AgentStreamChunk
                        {
                            Type = "tool_call",
                            ToolCallDelta = new ToolCallDelta
                            {
                                Index = toolCallDelta.Index,
                                Id = toolCallDelta.Id,
                                Type = toolCallDelta.Type,
                                Name = toolCallDelta.Function?.Name,
                                Arguments = toolCallDelta.Function?.Arguments
                            }
                        };
                    }
                }
            }

            // 流结束后判断是否有工具调用
            var hasToolCalls = toolCallAccumulators.Count > 0 &&
                (finishReason == "tool_calls" || finishReason == null && toolCallAccumulators.Count > 0);

            if (hasToolCalls)
            {
                var completedToolCalls = OAI.StreamToolCallHelper.ToToolCalls(toolCallAccumulators);

                // 追加 AssistantMessage（含 tool_calls）
                var assistantContent = contentBuilder.Length > 0 ? contentBuilder.ToString() : null;
                messages.Add(new OAI.AssistantMessage
                {
                    Content = assistantContent,
                    ToolCalls = completedToolCalls
                });

                // 执行工具调用
                foreach (var toolCall in completedToolCalls)
                {
                    var toolResult = await ExecuteToolCallAsync(toolCall, cancellationToken);
                    allToolResults.Add(toolResult);

                    yield return new AgentStreamChunk
                    {
                        Type = "tool_result",
                        ToolResult = toolResult
                    };

                    messages.Add(OAI.ChatMessage.Tool(toolCall.Id, toolResult.Content ?? string.Empty));
                }

                // 继续下一轮循环
            }
            else
            {
                // 无工具调用，流式结束
                var finalContent = contentBuilder.ToString();
                messages.Add(OAI.ChatMessage.Assistant(finalContent));

                var finalResponse = new AgentResponse
                {
                    Content = finalContent,
                    Model = responseModel,
                    ToolResults = allToolResults,
                    ConversationHistory = messages,
                    Iterations = iteration + 1,
                    StopReason = "completed",
                    TotalUsage = totalUsage
                };

                yield return new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = finalResponse
                };

                yield break;
            }
        }

        // 超出最大迭代次数
        var maxIterResponse = new AgentResponse
        {
            Content = string.Empty,
            Model = request.Model,
            ToolResults = allToolResults,
            ConversationHistory = messages,
            Iterations = iteration,
            StopReason = "max_iterations_reached",
            TotalUsage = totalUsage
        };

        yield return new AgentStreamChunk
        {
            Type = "done",
            FinalResponse = maxIterResponse
        };
    }

    // ------------------------------------------------------------------ //
    //  Private helpers
    // ------------------------------------------------------------------ //

    /// <summary>
    /// 构建 Pipeline 责任链（从后向前包裹 filter）
    /// </summary>
    private Func<OAI.ChatCompletionRequest, Task<OAI.ChatCompletionResponse>> BuildPipeline(
        AgentPipelineContext context, CancellationToken ct)
    {
        Func<OAI.ChatCompletionRequest, Task<OAI.ChatCompletionResponse>> pipeline =
            req => _chatCompatible.ChatAsync(req, ct);

        for (int i = _filters.Count - 1; i >= 0; i--)
        {
            var filter = _filters[i];
            var next = pipeline;
            pipeline = req => filter.InvokeAsync(req, context, next, ct);
        }

        return pipeline;
    }

    /// <summary>
    /// 构建初始消息列表（SystemMessage + ConversationHistory + UserMessage）
    /// </summary>
    private List<OAI.ChatMessage> BuildInitialMessages(AgentRequest request)
    {
        var messages = new List<OAI.ChatMessage>();

        // 构建 system prompt
        var systemPrompt = request.SystemPrompt ?? string.Empty;
        if (!string.IsNullOrEmpty(request.SkillName) &&
            _skills.TryGetValue(request.SkillName, out var skill) &&
            !string.IsNullOrEmpty(skill.SystemPrompt))
        {
            systemPrompt = string.IsNullOrEmpty(systemPrompt)
                ? skill.SystemPrompt
                : systemPrompt + "\n\n" + skill.SystemPrompt;
        }

        if (!string.IsNullOrEmpty(systemPrompt))
            messages.Add(OAI.ChatMessage.System(systemPrompt));

        // 追加对话历史
        if (request.ConversationHistory?.Count > 0)
            messages.AddRange(request.ConversationHistory);

        // 追加用户消息
        if (!string.IsNullOrEmpty(request.UserMessage))
            messages.Add(OAI.ChatMessage.User(request.UserMessage));

        return messages;
    }

    /// <summary>
    /// 将 Models.ToolDefinition 列表转换为 OAI.ToolDefinition 列表
    /// </summary>
    private List<OAI.ToolDefinition> BuildOaiTools()
    {
        var result = new List<OAI.ToolDefinition>();

        foreach (var executor in _tools.Values)
        {
            var modelDef = executor.ToolDefinition;
            OAI.FunctionParameters parameters = null;

            if (!string.IsNullOrEmpty(modelDef.Function?.Parameters))
            {
                try
                {
                    parameters = JsonSerializer.Deserialize<OAI.FunctionParameters>(
                        modelDef.Function.Parameters,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                catch
                {
                    // 解析失败则保留 null（不传 parameters）
                }
            }

            result.Add(new OAI.ToolDefinition
            {
                Type = modelDef.Type ?? "function",
                Function = new OAI.FunctionDefinition
                {
                    Name = modelDef.Function?.Name ?? string.Empty,
                    Description = modelDef.Function?.Description,
                    Parameters = parameters
                }
            });
        }

        return result;
    }

    /// <summary>
    /// 执行单个 OAI.ToolCall，返回 ToolResult
    /// </summary>
    private async Task<ToolResult> ExecuteToolCallAsync(
        OAI.ToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var functionName = toolCall.Function?.Name ?? string.Empty;

        if (!_tools.TryGetValue(functionName, out var executor))
        {
            return new ToolResult
            {
                ToolCallId = toolCall.Id,
                ToolName = functionName,
                Success = false,
                Content = $"未找到工具：{functionName}",
                ErrorCode = "TOOL_NOT_FOUND"
            };
        }

        try
        {
            var result = await executor.ExecuteAsync(
                toolCall.Function?.Arguments ?? string.Empty,
                cancellationToken);

            // 确保关联 ID 和名称
            result.ToolCallId ??= toolCall.Id;
            result.ToolName ??= functionName;
            return result;
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                ToolCallId = toolCall.Id,
                ToolName = functionName,
                Success = false,
                Content = ex.Message,
                ErrorCode = "TOOL_EXECUTION_ERROR"
            };
        }
    }
}
