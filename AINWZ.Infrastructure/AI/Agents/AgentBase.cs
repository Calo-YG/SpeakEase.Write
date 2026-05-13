using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

public abstract class AgentBase(
    IChatCompatible llm,
    IToolCapable tools,
    ILogger logger) : INovelAgent
{
    protected readonly IChatCompatible Llm = llm;
    protected readonly IToolCapable Tools = tools;
    protected readonly ILogger Logger = logger;

    private bool _toolsRegistered;

    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string BuildPrompt();

    public virtual AgentMetadata Metadata => new()
    {
        RouteKeywords = [],
        ContentType = "plain",
        NeedsProjectMemory = true,
        ShouldFilterHistory = false,
        DefaultParameters = AgentParameters.Default
    };

    public virtual string RouteDescription => DisplayName;

    public virtual void RegisterTools(IToolCapable toolCapable)
    {
        if (_toolsRegistered) return;
        foreach (var def in GetToolDefinitions())
            toolCapable.RegisterTool(def);
        _toolsRegistered = true;
    }

    protected abstract IEnumerable<ToolDefinition> GetToolDefinitions();

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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

        RegisterTools(Tools);

        var messages = BuildMessages(request);
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

        for (var i = 0; i < request.MaxIterations; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation("[{Agent}] 迭代 {Iter} 被取消", Name, i + 1);
                break;
            }

            var iterStopwatch = Stopwatch.StartNew();
            LLMTurnResult turnResult = null;

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

            if (turnResult.HasToolCalls)
            {
                Logger.LogDebug("[{Agent}] 迭代 {Iter} 调用 {ToolCount} 个工具, elapsed={Elapsed}ms",
                    Name, i + 1, turnResult.ToolCalls.Count, iterStopwatch.ElapsedMilliseconds);

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
                    messages.Add(ChatMessage.Tool(tc.Id, tr.Content ?? string.Empty));
                }
            }
            else
            {
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
                        StopReason = "completed"
                    }
                };
                yield break;
            }
        }

        Logger.LogWarning("[{Agent}] 达到最大迭代次数 {MaxIter}, elapsed={Elapsed}ms",
            Name, request.MaxIterations, agentStopwatch.ElapsedMilliseconds);

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
