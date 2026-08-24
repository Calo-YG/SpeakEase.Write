using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Agents;

// 小说 Agent 兼容基类：能力由具体 Agent 声明，执行循环统一委托给 AgentLoop。
public abstract class AgentBase(
    IChatCompatible llm,
    IToolCapable tools,
    ILogger logger) : INovelAgent
{
    protected readonly IChatCompatible Llm = llm;
    protected readonly IToolCapable Tools = tools;
    protected readonly ILogger Logger = logger;

    private readonly AgentLoop _agentLoop = new();
    private bool _toolsRegistered;

    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string BuildPrompt();

    public virtual PromptProfile BuildPromptProfile() => new()
    {
        Identity = BuildPrompt()
    };

    public virtual AgentMetadata Metadata => new()
    {
        ContentType = "plain",
        NeedsProjectMemory = true,
        ShouldFilterHistory = false,
        DefaultParameters = AgentParameters.Default
    };

    public virtual string RouteDescription => DisplayName;

    public virtual void RegisterTools(IToolCapable toolCapable)
    {
        if (_toolsRegistered)
            return;

        foreach (var definition in GetToolDefinitions())
            toolCapable.RegisterTool(definition);

        _toolsRegistered = true;
    }

    protected abstract IEnumerable<ToolDefinition> GetToolDefinitions();

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            Logger.LogWarning("[{Agent}] 请求校验失败: {Error}", Name, validationError);
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

        RegisterTools(Tools);

        await foreach (var chunk in _agentLoop.RunAsync(new AgentLoopRequest
        {
            RunId = request.RunId,
            StepId = request.StepId,
            AgentName = Name,
            Llm = Llm,
            Tools = Tools,
            Journal = request.Journal,
            Request = request
        }, cancellationToken))
        {
            yield return chunk;
        }
    }

    private static string ValidateRequest(AgentRequest request)
    {
        if (request is null)
            return "Request cannot be null";

        if (string.IsNullOrWhiteSpace(request.SystemPrompt) &&
            string.IsNullOrWhiteSpace(request.UserMessage))
        {
            return "SystemPrompt 和 UserMessage 不能同时为空";
        }

        if (request.MaxIterations < 1)
            return $"MaxIterations 必须 >= 1, 当前值: {request.MaxIterations}";

        if (request.MaxIterations > 50)
            return $"MaxIterations 不能超过 50, 当前值: {request.MaxIterations}";

        return null;
    }
}
