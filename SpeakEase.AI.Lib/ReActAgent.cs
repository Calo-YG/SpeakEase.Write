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
    /// ReAct Agent 默认系统提示词。
    /// 对齐 Function Calling 机制，引导 LLM 通过工具调用与直接回答协作完成任务。
    /// </summary>
    private const string SystemPropmt = @"# 角色
你是 AI 智能助手，具备工具调用能力。通过 Function Calling 调用工具获取外部信息，也可基于自身知识直接回答。

# 决策流程
面对每个请求，按以下步骤决策：

1. **分析需求** — 用户想要什么？我的知识是否足以直接回答？
2. **选择行动** —
   - 需要实时信息、计算或外部操作 → 调用对应工具
   - 已有足够信息 → 直接回答
3. **评估结果** — 工具返回后：
   - 满足需求 → 组织最终回答
   - 信息不足 → 补充调用其他工具
   - 调用失败 → 分析原因，换一种方式，不要重复相同调用

# 工具使用原则
- 工具通过 Function Calling 机制调用，你只需选择工具并填写参数
- 禁止在回复中虚构文本格式的工具调用（如 `Action: tool_call xxx`）
- 需要的能力不在已有工具中时，调用 find_skill 查找更多技能
- 多步任务可依次调用工具，后一步可依赖前一步结果
- 工具失败时，向用户说明情况并给出基于现有信息的最佳回答

# 技能调用方式
技能（如 Agent Browser）是 CLI 工具，通过 run_powershell 执行其命令来调用：
1. 先用 find_skill 获取技能的完整使用文档
2. 然后通过 run_powershell 执行技能的 CLI 命令

示例：使用 Agent Browser 打开网页
- 调用 find_skill，传入 skillName=""Agent Browser""，获取完整命令参考
- 调用 run_powershell，传入 command=""agent-browser open https://example.com""
- 调用 run_powershell，传入 command=""agent-browser snapshot -i"" 获取页面元素

# 输出规范
- 直接用自然语言回答用户，无需输出 Thought/Action/Observation 等格式
- 回答要准确、完整、有条理
- 不确定的信息标注""推测""，无来源不断言

# 约束
1. **先判断再行动** — 简单问题直接回答，不要为了调用工具而调用工具
2. **失败换路** — 工具出错时分析原因，禁止相同参数重复调用
3. **及时收敛** — 信息充足后立即回答，禁止过度调用工具
4. **最多 10 轮** — 达到上限后基于当前信息给出最佳进展";

    /// <summary>
    /// 注册守卫，确保工具和技能仅注册一次。
    /// </summary>
    private bool _initialized;

    /// <summary>
    /// 注册工具和技能：仅首次调用时执行，后续调用直接返回。
    /// </summary>
    public void Init()
    {
        // 已初始化则跳过，避免重复注册工具和技能
        if (_initialized) return;
        _initialized = true;

        // 注册所有内置工具：Echo、角色名生成、数学计算、时间、PowerShell、随机数、文本分析、技能查找
        toolCapable.RegisterTool(EchoTool.ToolDefinition);
        toolCapable.RegisterTool(CharacterNameGeneratorTool.ToolDefinition);
        toolCapable.RegisterTool(CalculateTool.ToolDefinition);
        toolCapable.RegisterTool(GetCurrentTimeTool.ToolDefinition);
        toolCapable.RegisterTool(PowerShellTool.ToolDefinition);
        toolCapable.RegisterTool(RandomGeneratorTool.ToolDefinition);
        toolCapable.RegisterTool(TextAnalyzerTool.ToolDefinition);
        toolCapable.RegisterTool(SkillFindTool.ToolDefinition);
        // 注册内置技能：Agent Browser 无头浏览器自动化
        skilCapable.RegiSkill(new SkillDefinition { Description = "无头浏览器自动化，支持网页导航、点击、输入、截图，内置 PowerShell 执行和网络搜索能力", Name = "Agent Browser", Path = @"wwwroot\skills\agent-browser-0.2.0\SKILL.md" });
    }

    /// <inheritdoc />
    public async Task<AgentResponse> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 初始化累加器和辅助变量
        var totalUsage = new UsageInfo();
        var allToolResults = new List<ToolResult>();
        int iteration = 0;

        // 确保工具和技能已注册
        Init();

        // 构建消息列表：SystemPrompt + Skill 摘要 + UserMessage + 历史对话
        var messages = BuildMessages(request);

        // 构建 ReAct 循环内每次迭代不变的 LLM 上下文（模型、温度、最大 Token）
        var turnContext = new LLMTurnContext
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens
        };

        // === ReAct 主循环 ===
        // 每轮迭代：调用 LLM → 检查结果 → 执行工具调用 / 直接回答
        for (; iteration < request.MaxIterations; iteration++)
        {
            // 调用 LLM 策略执行单轮对话
            var turnResult = await llmStrategy.ChatAsync(turnContext, messages, toolCapable.Tools, cancellationToken);

            // 累加本轮的 Token 用量到总用量
            AccumulateUsage(totalUsage, turnResult.Usage);

            // LLM 调用失败，返回空结果并标记错误原因
            if (!turnResult.Success)
            {
                return new AgentResponse
                {
                    Content = string.Empty,
                    Model = turnResult.Model ?? request.Model,
                    Iterations = iteration + 1,
                    StopReason = "llm_error",
                    TotalUsage = totalUsage
                };
            }

            // LLM 返回了工具调用请求
            if (turnResult.HasToolCalls)
            {
                // 将包含 tool_calls 的 Assistant 消息添加到对话历史
                messages.Add(new AssistantMessage
                {
                    Content = turnResult.Content ?? string.Empty,
                    ToolCalls = turnResult.ToolCalls
                });

                // 依次执行每个工具调用，并将结果作为 ToolMessage 回填到对话历史
                foreach (var toolCall in turnResult.ToolCalls)
                {
                    var toolResult = await toolCapable.ExecuteAsync(toolCall, cancellationToken);
                    allToolResults.Add(toolResult);
                    // 工具执行结果以 ToolMessage 形式追加，LLM 在下一轮可据此继续推理
                    messages.Add(ChatMessage.Tool(toolCall.Id, toolResult.Content ?? string.Empty));
                }
                // 继续下一轮循环，让 LLM 根据工具结果决定下一步
            }
            else
            {
                // LLM 直接给出最终回答，循环结束
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

        // 超出最大迭代次数仍未得到最终回答
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

        // === ReAct 流式循环 ===
        // 与非流式逻辑一致，但每轮的结果通过 IAsyncEnumerable 逐块 yield 给调用方
        for (; iteration < request.MaxIterations; iteration++)
        {
            LLMTurnResult turnResult = null;

            // 消费 LLM 流式响应，逐块转发给上层
            await foreach (var turnChunk in llmStrategy.StreamAsync(turnContext, messages, toolCapable.Tools, cancellationToken))
            {
                switch (turnChunk.Type)
                {
                    case "content":
                        // 文本增量：直接转发
                        yield return new AgentStreamChunk
                        {
                            Type = "content",
                            Content = turnChunk.Content
                        };
                        break;

                    case "tool_call":
                        // 工具调用增量：转发给前端展示
                        yield return new AgentStreamChunk
                        {
                            Type = "tool_call",
                            ToolCallDelta = turnChunk.ToolCallDelta
                        };
                        break;

                    case "done":
                        // 单轮 LLM 交互完成，获取完整结果
                        turnResult = turnChunk.TurnResult;
                        break;
                }
            }

            // 累加本轮的 Token 用量
            AccumulateUsage(totalUsage, turnResult?.Usage);

            if (turnResult is null)
                continue;

            // LLM 调用失败，发送错误信息和最终响应
            if (!turnResult.Success)
            {
                var errorMessage = string.IsNullOrWhiteSpace(turnResult.ErrorMessage)
                    ? "LLM call failed."
                    : turnResult.ErrorMessage;

                yield return new AgentStreamChunk { Type = "error", Content = errorMessage };
                yield return new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = new AgentResponse
                    {
                        Content = string.Empty,
                        Model = turnResult.Model ?? request.Model,
                        Iterations = iteration + 1,
                        StopReason = "llm_error",
                        TotalUsage = totalUsage
                    }
                };
                yield break;
            }

            // LLM 返回了工具调用请求
            if (turnResult.HasToolCalls)
            {
                // 将包含 tool_calls 的 Assistant 消息追加到对话历史
                messages.Add(new AssistantMessage
                {
                    Content = turnResult.Content ?? string.Empty,
                    ToolCalls = turnResult.ToolCalls
                });

                // 执行工具调用并实时推送工具结果
                foreach (var toolCall in turnResult.ToolCalls)
                {
                    var toolResult = await toolCapable.ExecuteAsync(toolCall, cancellationToken);
                    allToolResults.Add(toolResult);

                    // 流式推送工具执行结果
                    yield return new AgentStreamChunk
                    {
                        Type = "tool_result",
                        ToolResult = toolResult
                    };

                    // 将工具结果追加到对话历史供下一轮 LLM 调用
                    messages.Add(ChatMessage.Tool(toolCall.Id, toolResult.Content ?? string.Empty));
                }

                // 继续下一轮循环
            }
            else
            {
                // LLM 直接给出最终回答，流式结束
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
        // 增量为空时跳过（流式场景下 usage 可能为 null）
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

        // 合并 SystemPrompt：请求指定的提示词（或默认）+ Skill 摘要
        var systemPrompt = BuildSystemPrompt(request);

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(ChatMessage.System(systemPrompt));
        }

        // 追加历史对话消息（多轮对话上下文）
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

        // 优先使用请求指定的 SystemPrompt，否则使用默认的 ReAct 系统提示词
        var systemPrompt = request.SystemPrompt ?? SystemPropmt;

        if (string.IsNullOrEmpty(request.SystemPrompt))
        {
            systemPrompt = SystemPropmt;
        }

        sb.Append(systemPrompt);

        // 追加已注册技能摘要，让 LLM 知道可用的技能
        var skillPrompt = skilCapable.BuildSkillPropmt();

        if (!string.IsNullOrEmpty(skillPrompt))
        {
            // 确保技能提示词与系统提示词之间有换行
            if (sb.Length > 0) sb.AppendLine();

            sb.Append(skillPrompt);
        }

        return sb.ToString();
    }
}
