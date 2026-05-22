using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

// Agent基类，封装ReAct推理-行动循环的通用逻辑，所有小说创作Agent均继承此类
public abstract class AgentBase(
    IChatCompatible llm,
    IToolCapable tools,
    ILogger logger) : INovelAgent
{
    protected readonly IChatCompatible Llm = llm; // LLM客户端，用于与AI模型通信
    protected readonly IToolCapable Tools = tools; // 工具执行器，用于调用Agent定义的工具
    protected readonly ILogger Logger = logger; // 日志记录器

    private bool _toolsRegistered; // 标记工具是否已注册，避免重复注册

    public abstract string Name { get; } // Agent唯一标识名称
    public abstract string DisplayName { get; } // Agent显示名称
    public abstract string BuildPrompt(); // 构建Agent的系统提示词

    // Agent元数据：内容类型、是否需要项目记忆、是否过滤历史、默认LLM参数
    public virtual AgentMetadata Metadata => new()
    {
        ContentType = "plain",
        NeedsProjectMemory = true,
        ShouldFilterHistory = false,
        DefaultParameters = AgentParameters.Default
    };

    public virtual string RouteDescription => DisplayName; // Agent功能描述，用于路由匹配

    // 注册Agent所需的工具定义，仅执行一次（幂等操作）
    public virtual void RegisterTools(IToolCapable toolCapable)
    {
        if (_toolsRegistered) return;
        foreach (var def in GetToolDefinitions())
            toolCapable.RegisterTool(def);
        _toolsRegistered = true;
    }

    // 获取该Agent需要的工具定义列表，由子类覆写
    protected abstract IEnumerable<ToolDefinition> GetToolDefinitions();

    // 核心方法：流式执行ReAct推理-行动循环
    // 循环内依次执行：LLM推理 → 解析工具调用 → 执行工具 → 将结果反馈给LLM，直到无工具调用或达到最大迭代次数
    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 参数校验：确保SystemPrompt/UserMessage至少有一个，MaxIterations在1-50之间
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            Logger.LogWarning("[{Agent}] 请求校验失败: {Error}", Name, validationError);
            yield return new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = new AgentResponse
                {
                    Content = string.Empty,
                    StopReason = "invalid_request",
                }
            };
            yield break;
        }

        // 注册工具并构建初始消息列表
        RegisterTools(Tools);

        var messages = BuildMessages(request);

        // 构建LLM调用上下文（温度、TopP、频率惩罚等参数）
        var ctx = new LLMTurnContext
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            TopP = request.TopP,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty
        };

        var agentStopwatch = Stopwatch.StartNew();
        Logger.LogInformation("[{Agent}] 开始执行, model={Model}, maxIter={MaxIter}, msgCount={MsgCount}",
            Name, request.Model, request.MaxIterations, messages.Count);

        // ReAct主循环：最多迭代MaxIterations次
        for (var i = 0; i < request.MaxIterations; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("[{Agent}] 迭代 {Iter} 被取消", Name, i + 1);
                break;
            }

            var iterStopwatch = Stopwatch.StartNew();
            LLMTurnResult turnResult = null;

            // 流式调用LLM，逐块接收content/reasoning/tool_call/done事件
            await foreach (var tc in Llm.StreamAsync(ctx, messages, Tools.Tools, cancellationToken))
            {
                switch (tc.Type)
                {
                    case "content":
                        yield return new AgentStreamChunk { Type = "content", Content = tc.Content };
                        break;
                    case "reasoning":
                        yield return new AgentStreamChunk { Type = "reasoning", Content = tc.Content };
                        break;
                    case "tool_call":
                        yield return new AgentStreamChunk { Type = "tool_call", ToolCallDelta = tc.ToolCallDelta };
                        break;
                    case "done":
                        turnResult = tc.TurnResult;
                        break;
                }
            }

            if (turnResult == null)
            {
                Logger.LogWarning("[{Agent}] 迭代 {Iter} LLM 未返回结果，跳过", Name, i + 1);
                continue;
            }

            // LLM调用失败：返回错误信息并终止执行
            if (!turnResult.Success)
            {
                var errorMessage = string.IsNullOrWhiteSpace(turnResult.ErrorMessage)
                    ? "LLM call failed."
                    : turnResult.ErrorMessage;

                Logger.LogWarning("[{Agent}] LLM call failed at iteration {Iter}: {Error}", Name, i + 1, errorMessage);
                yield return new AgentStreamChunk { Type = "error", Content = errorMessage };
                yield return new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = new AgentResponse
                    {
                        Content = string.Empty,
                        Model = turnResult.Model ?? request.Model,
                        Iterations = i + 1,
                        StopReason = "llm_error"
                    }
                };
                yield break;
            }

            // LLM返回了工具调用：将Assistant消息加入上下文，依次执行工具并反馈结果
            if (turnResult.HasToolCalls)
            {
                Logger.LogDebug("[{Agent}] 迭代 {Iter} 调用 {ToolCount} 个工具, elapsed={Elapsed}ms",
                    Name, i + 1, turnResult.ToolCalls.Count, iterStopwatch.ElapsedMilliseconds);

                // 将带工具调用的Assistant消息加入对话历史
                messages.Add(new AssistantMessage
                {
                    Content = turnResult.Content ?? string.Empty,
                    ReasoningContent = turnResult.ReasoningContent,
                    ToolCalls = turnResult.ToolCalls
                });
                foreach (var tc in turnResult.ToolCalls)
                {
                    var toolStopwatch = Stopwatch.StartNew();
                    var toolName = tc.Function?.Name ?? "unknown";
                    ToolResult tr;
                    try
                    {
                        tr = await Tools.ExecuteAsync(tc, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "[{Agent}] 工具 {Tool} 执行异常", Name, toolName);
                        tr = ToolResult.Fail($"工具执行异常: {ex.Message}", "tool_execution_error");
                    }

                    Logger.LogDebug("[{Agent}] 工具 {Tool} 执行完成, success={Success}, elapsed={Elapsed}ms",
                        Name, toolName, tr.Success, toolStopwatch.ElapsedMilliseconds);

                    yield return new AgentStreamChunk { Type = "tool_result", ToolResult = tr };
                    // 将工具执行结果（Tool消息）加入对话历史，供LLM下一轮迭代参考
                    messages.Add(ChatMessage.Tool(tc.Id, tr.Content ?? string.Empty));
                }
            }
            else
            {
                // LLM不再调用工具，认为任务已完成：返回最终文本内容
                Logger.LogInformation("[{Agent}] 迭代 {Iter} 完成, elapsed={Elapsed}ms, reason=无工具调用,结束循环",
                    Name, i + 1, iterStopwatch.ElapsedMilliseconds);

                messages.Add(ChatMessage.Assistant(turnResult.Content, turnResult.ReasoningContent));
                yield return new AgentStreamChunk
                {
                    Type = "done",
                    FinalResponse = new AgentResponse
                    {
                        Content = turnResult.Content,
                        ReasoningContent = turnResult.ReasoningContent,
                        Model = turnResult.Model,
                        Iterations = i + 1,
                        StopReason = "completed",
                        TotalUsage = turnResult.Usage
                    }
                };
                yield break;
            }
        }

        // 循环耗尽：达到最大迭代次数仍未完成任务
        Logger.LogWarning("[{Agent}] 达到最大迭代次数 {MaxIter}, elapsed={Elapsed}ms", Name, request.MaxIterations, agentStopwatch.ElapsedMilliseconds);

        yield return new AgentStreamChunk
        {
            Type = "done",
            FinalResponse = new AgentResponse
            {
                Content = string.Empty,
                Model = request.Model,
                Iterations = request.MaxIterations,
                StopReason = "max_iterations_reached"
            }
        };
    }

    // 校验请求参数：确保提示词非空、迭代次数在合理范围(1-50)
    private static string ValidateRequest(AgentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SystemPrompt) && string.IsNullOrWhiteSpace(req.UserMessage))
            return "SystemPrompt 和 UserMessage 不能同时为空";

        if (req.MaxIterations < 1)
            return $"MaxIterations 必须 >= 1, 当前值: {req.MaxIterations}";

        if (req.MaxIterations > 50)
            return $"MaxIterations 不能超过 50, 当前值: {req.MaxIterations}";

        return null;
    }

    // 构建发送给LLM的完整消息列表：SystemPrompt → 对话历史 → UserMessage
    private static List<ChatMessage> BuildMessages(AgentRequest req)
    {
        var msgs = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(req.SystemPrompt))
            msgs.Add(ChatMessage.System(req.SystemPrompt));

        if (req.ConversationHistory?.Count > 0)
            msgs.AddRange(req.ConversationHistory);

        if (!string.IsNullOrEmpty(req.UserMessage))
            msgs.Add(ChatMessage.User(req.UserMessage));

        return msgs;
    }
}
