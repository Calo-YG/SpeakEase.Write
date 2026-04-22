using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Tools;
using System.Runtime.CompilerServices;
using System.Text;


namespace SpeakEase.AI.Lib;

/// <summary>
/// ReAct 模式 Agent 实现，支持 Tool 注册、Skill 注册和流式/非流式执行。
/// 通过 ILLMStrategy 与 LLM 交互，自身只关注对话轮次编排逻辑。
/// </summary>
public sealed class ReActAgent(IToolCapable toolCapable, ISkilCapable skilCapable, IChatCompatible llmStrategy) : IReActAgent
{
    /// <summary>
    /// 
    /// </summary>
    private const string SystemPropmt = @"# 角色
你是 ReAct Agent，通过""思考→行动→观察""循环解决问题。

# 循环格式
每轮必须输出：

Thought: [现状分析] → [还缺什么] → [下一步策略] → [是否已能回答]
Action: [tool_call/calculate/reasoning/ask_user/final_answer] [具体内容]
Observation: [行动结果与评估]

# 行动类型
- **tool_call** [工具名] [参数] — 调用外部工具
- **calculate** [表达式] — 数学/逻辑计算  
- **reasoning** [推导] — 纯逻辑推演
- **ask_user** [问题] — 需用户补充信息
- **final_answer** — 给出最终答案（终止循环）

# 铁律
1. **先思后行** — 无 Thought 不得 Action
2. **失败换路** — 工具失败必须分析原因，禁止重复调用
3. **及时收敛** — 信息足够立即 final_answer，禁止过度循环
4. **上限 10 轮** — 达限输出当前最佳进展
5. **零幻觉** — 不确定标注""推测""，无来源不断言

# 示例
用户：北京今天天气？
Round 1
Thought: 需实时天气，训练数据无今日信息，必须调工具。风险：接口可能失败。
Action: tool_call weather_api location=""北京""
Observation: 返回晴，15-26°C，北风3级。
Round 2
Thought: 信息完整，可直接回答。
Action: final_answer
北京今天晴，15-26°C，北风3级，空气质量优。";

    /// <summary>
    /// 手动注册工具和技能：ToolDefinition
    /// </summary>
    public void Init()
    {
        // 手动注册工具和技能：IToolExecutor 的实现本身需要构建一个静态常量 ToolDefinition 后续这里只要通过 IToolExecutor.ToolDefinition 获取并注册
        toolCapable.RegisterTool(EchoTool.ToolDefinition);
        toolCapable.RegisterTool(CharacterNameGeneratorTool.ToolDefinition);
        toolCapable.RegisterTool(CalculateTool.ToolDefinition);
        toolCapable.RegisterTool(GetCurrentTimeTool.ToolDefinition);
        toolCapable.RegisterTool(PowerShellTool.ToolDefinition);
        toolCapable.RegisterTool(RandomGeneratorTool.ToolDefinition);
        toolCapable.RegisterTool(TextAnalyzerTool.ToolDefinition);
    }

    /// <inheritdoc />
    public async Task<AgentResponse> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var totalUsage = new UsageInfo();
        var allToolResults = new List<ToolResult>();
        int iteration = 0;

        Init();

        // 构建消息列表：SystemPrompt + Skill 摘要 + UserMessage + 历史对话
        var messages = BuildMessages(request);

        // 构建不变上下文
        var turnContext = new LLMTurnContext
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens
        };

        // ReAct 循环
        for (; iteration < request.MaxIterations; iteration++)
        {
            var turnResult = await llmStrategy.ChatAsync(turnContext, messages, toolCapable.Tools, cancellationToken);

            // 累加 usage
            AccumulateUsage(totalUsage, turnResult.Usage);

            if (turnResult.HasToolCalls)
            {
                // 追加 AssistantMessage（含 tool_calls）
                messages.Add(new AssistantMessage
                {
                    Content = turnResult.Content,
                    ToolCalls = turnResult.ToolCalls
                });

                // 执行每个工具调用并追加 ToolMessage
                foreach (var toolCall in turnResult.ToolCalls)
                {
                    var toolResult = await toolCapable.ExecuteAsync(toolCall, cancellationToken);
                    allToolResults.Add(toolResult);
                    messages.Add(ChatMessage.Tool(toolCall.Id, toolResult.Content ?? string.Empty));
                }
            }
            else
            {
                // 无 tool_calls，循环结束
                messages.Add(ChatMessage.Assistant(turnResult.Content));

                return new AgentResponse
                {
                    Content = turnResult.Content,
                    Model = turnResult.Model,
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

        Init();

        // 构建消息列表：SystemPrompt + Skill 摘要 + UserMessage + 历史对话
        var messages = BuildMessages(request);

        // 构建不变上下文
        var turnContext = new LLMTurnContext
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens
        };

        var totalUsage = new UsageInfo();
        var allToolResults = new List<ToolResult>();
        int iteration = 0;

        for (; iteration < request.MaxIterations; iteration++)
        {
            LLMTurnResult turnResult = null;

            await foreach (var turnChunk in llmStrategy.StreamAsync(turnContext, messages, toolCapable.Tools, cancellationToken))
            {
                switch (turnChunk.Type)
                {
                    case "content":
                        yield return new AgentStreamChunk
                        {
                            Type = "content",
                            Content = turnChunk.Content
                        };
                        break;

                    case "tool_call":
                        yield return new AgentStreamChunk
                        {
                            Type = "tool_call",
                            ToolCallDelta = turnChunk.ToolCallDelta
                        };
                        break;

                    case "done":
                        turnResult = turnChunk.TurnResult;
                        break;
                }
            }

            // 累加 usage
            AccumulateUsage(totalUsage, turnResult?.Usage);

            if (turnResult is null)
                continue;

            if (turnResult.HasToolCalls)
            {
                // 追加 AssistantMessage（含 tool_calls）
                messages.Add(new AssistantMessage
                {
                    Content = turnResult.Content,
                    ToolCalls = turnResult.ToolCalls
                });

                // 执行工具调用
                foreach (var toolCall in turnResult.ToolCalls)
                {
                    var toolResult = await toolCapable.ExecuteAsync(toolCall, cancellationToken);
                    allToolResults.Add(toolResult);

                    yield return new AgentStreamChunk
                    {
                        Type = "tool_result",
                        ToolResult = toolResult
                    };

                    messages.Add(ChatMessage.Tool(toolCall.Id, toolResult.Content ?? string.Empty));
                }

                // 继续下一轮循环
            }
            else
            {
                // 无工具调用，流式结束
                messages.Add(ChatMessage.Assistant(turnResult.Content));

                var finalResponse = new AgentResponse
                {
                    Content = turnResult.Content,
                    Model = turnResult.Model,
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

    /// <summary>
    /// 累加 Token 用量
    /// </summary>
    private static void AccumulateUsage(UsageInfo total, UsageInfo increment)
    {
        if (increment is null) return;
        total.PromptTokens += increment.PromptTokens;
        total.CompletionTokens += increment.CompletionTokens;
        total.TotalTokens += increment.TotalTokens;
    }

    /// <summary>
    /// 构建初始消息列表：SystemPrompt（含 Skill 摘要）+ 历史对话 + UserMessage
    /// </summary>
    private List<ChatMessage> BuildMessages(AgentRequest request)
    {
        var messages = new List<ChatMessage>();

        // 合并 SystemPrompt：请求级 + Skill 摘要
        var systemPrompt = BuildSystemPrompt(request);
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(ChatMessage.System(systemPrompt));
        }

        // 追加历史对话
        if (request.ConversationHistory?.Count > 0)
        {
            messages.AddRange(request.ConversationHistory);
        }

        // 追加当前用户消息
        if (!string.IsNullOrEmpty(request.UserMessage))
        {
            messages.Add(ChatMessage.User(request.UserMessage));
        }

        return messages;
    }

    /// <summary>
    /// 合并系统提示词：请求 SystemPrompt + Skill 摘要
    /// </summary>
    private string BuildSystemPrompt(AgentRequest request)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            sb.Append(request.SystemPrompt);
        }

        var skillPrompt = skilCapable.BuildSkillPropmt();
        if (!string.IsNullOrEmpty(skillPrompt))
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(skillPrompt);
        }

        return sb.ToString();
    }
}
