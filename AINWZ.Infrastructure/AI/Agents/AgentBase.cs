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
    ILogger logger) : INovelAgent, IAgentDefinition
{
    protected readonly IChatCompatible Llm = llm;
    protected readonly IToolCapable Tools = tools;
    protected readonly ILogger Logger = logger;

    private readonly AgentLoop _agentLoop = new();
    private bool _toolsRegistered;

    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string BuildPrompt();

    public virtual AgentDescriptor Descriptor => new()
    {
        Name = Name,
        DisplayName = DisplayName,
        Domain = $"novel.{Name}",
        OutputKind = Metadata.ContentType,
        PromptProfileKey = $"novel.{Name}",
        PolicyProfileKey = "default",
        ToolGroups = Array.Empty<string>(),
        MemoryScopes = Metadata.NeedsProjectMemory
            ? new[] { "session", "project" }
            : new[] { "session" }
    };

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

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteRuntimeStreamAsync(
        AgentRequest request,
        IAgentRuntimeRunner runner,
        bool enableDynamicToolExposure,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            yield return new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = new AgentResponse { Content = string.Empty, StopReason = "invalid_request" }
            };
            yield break;
        }

        RegisterTools(Tools);
        var exposedTools = enableDynamicToolExposure ? SelectExposedTools() : Tools;
        await foreach (var runtimeEvent in runner.RunAsync(new RuntimeRunRequest
        {
            PublishEvents = false,
            LoopRequest = new AgentLoopRequest
            {
                RunId = request.RunId,
                StepId = request.StepId,
                AgentName = Name,
                Llm = Llm,
                Tools = exposedTools,
                Journal = request.Journal,
                Request = request
            }
        }, cancellationToken))
        {
            if (runtimeEvent.Chunk is not null)
                yield return runtimeEvent.Chunk;
        }
    }

    private IToolCapable SelectExposedTools()
    {
        var registry = new ToolRegistry();
        foreach (var tool in Tools.Tools)
            registry.Register(tool);

        var selected = new ToolExposurePolicy(registry).Select(new ToolExposureContext
        {
            AgentName = Name,
            Phase = "run",
            AllowedGroups = Descriptor.ToolGroups,
            PreferredTools = GetPreferredToolNames(Name),
            HasExplicitConsent = false,
            MaxTools = 12
        });
        return new ExposedToolCapable(Tools, selected);
    }

    private static IReadOnlyList<string> GetPreferredToolNames(string agentName)
        => agentName switch
        {
            "write" => new[]
            {
                "get_work_info", "get_outline", "get_recent_chapters", "get_writing_rules",
                "get_character", "get_world_setting", "get_foreshadowing", "get_timeline_events",
                "get_relationships", "save_chapter_content", "update_chapter_summary", "create_timeline_event"
            },
            "world" => new[]
            {
                "get_work_info", "get_world_setting", "search_world_setting", "get_power_system",
                "get_world_rules", "get_factions", "get_geography", "get_historical_events",
                "save_world_setting", "create_power_system", "create_world_rule", "create_historical_event"
            },
            "outline" => new[]
            {
                "get_work_info", "get_outline", "search_outline", "get_character_list",
                "get_world_setting", "get_foreshadowing", "get_timeline_events", "create_outline",
                "create_outline_node", "create_chapter_outline", "create_foreshadowing", "create_timeline_event"
            },
            _ => Array.Empty<string>()
        };

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
