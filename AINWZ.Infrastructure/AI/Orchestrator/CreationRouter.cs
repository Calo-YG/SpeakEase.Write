using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

/// <summary>
/// 创作路由决策，支持关键词匹配 + LLM 意图分类两阶段路由。
/// </summary>
public sealed class CreationRouter(IServiceScopeFactory scopeFactory, ILogger<CreationRouter> logger)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<CreationRouter> _logger = logger;
    private static readonly List<(string Keyword, string Agent, string ContentType)> KeywordRules = new()
    {
        ("续写",    "write",    "chapter"),
        ("润色",    "write",    "chapter"),
        ("改写",    "write",    "chapter"),
        ("扩写",    "write",    "chapter"),
        ("重写",    "write",    "chapter"),
        ("写一",    "write",    "chapter"),
        ("帮我写",  "write",    "chapter"),
        ("章节",    "write",    "chapter"),
        ("正文",    "write",    "chapter"),

        ("大纲",    "outline",  "outline"),
        ("情节",    "outline",  "outline"),
        ("规划",    "outline",  "outline"),
        ("结构",    "outline",  "outline"),
        ("高潮",    "outline",  "outline"),
        ("转折",    "outline",  "outline"),

        ("世界观",  "world",    "setting"),
        ("设定",    "world",    "setting"),
        ("势力",    "world",    "setting"),
        ("地理",    "world",    "setting"),

        ("角色",    "creation", "character"),
        ("人物",    "creation", "character"),
        ("创建",    "creation", "character"),
        ("新增",    "creation", "character"),

        ("创意",    "creation", "plain"),
        ("点子",    "creation", "plain"),
        ("脑洞",    "creation", "plain"),
        ("灵感",    "creation", "plain"),
        ("生成",    "creation", "plain"),

        ("检查",    "audit",    "audit_report"),
        ("审阅",    "audit",    "audit_report"),
        ("审核",    "audit",    "audit_report"),
        ("审查",    "audit",    "audit_report"),
        ("一致",    "audit",    "audit_report"),
        ("漏洞",    "audit",    "audit_report"),
        ("矛盾",    "audit",    "audit_report"),

        ("写",      "write",    "chapter"),
        ("世界",    "world",    "setting"),
        ("设计",    "creation", "character"),
    };

    private static readonly Dictionary<string, string> AgentNameMap = new()
    {
        ["write"] = "write",
        ["writer"] = "write",
        ["outline"] = "outline",
        ["world"] = "world",
        ["creation"] = "creation",
        ["audit"] = "audit"
    };

    public static RouteResult Decide(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return new RouteResult
            {
                AgentName = "write",
                ContentType = "plain",
                Reason = "空输入，默认路由到写作Agent"
            };

        foreach (var (keyword, agent, contentType) in KeywordRules)
        {
            if (userMessage.Contains(keyword))
            {
                return new RouteResult
                {
                    AgentName = agent,
                    ContentType = contentType,
                    Reason = $"关键词「{keyword}」匹配 → {agent}"
                };
            }
        }

        return new RouteResult
        {
            AgentName = "write",
            ContentType = "plain",
            Reason = "未匹配到关键词，默认路由到写作Agent"
        };
    }

    public async Task<RouteResult> DecideWithLLMAsync(string userMessage, CancellationToken ct = default)
    {
        var keywordResult = Decide(userMessage);

        if (keywordResult.AgentName != "write" || userMessage.Length <= 15)
            return keywordResult;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var llmContext = scope.ServiceProvider.GetRequiredService<IOpenAIContext>();
            await llmContext.ResolveAsync(ct);

            var llm = scope.ServiceProvider.GetRequiredService<IChatCompatible>();
            var turnContext = new LLMTurnContext
            {
                Model = llmContext.Model,
                Temperature = 0.1
            };

            var messages = new List<ChatMessage>
            {
                ChatMessage.System("""
你是一个意图分类器，负责将用户输入分类为以下 Agent：
- write: 写作/续写/润色/扩写章节正文
- outline: 管理大纲/情节规划/章节结构
- world: 管理世界观/世界设定/势力/地理
- creation: 创建角色/人物设计/创意灵感
- audit: 检查一致性/审查漏洞/发现矛盾

用户可能包含多个意图（如"帮我写完这章然后检查一致性"），请识别并返回 pipeline。

返回 JSON 对象，格式：
单一意图：{"agent": "<agent_name>", "reason": "<简短原因>"}
多意图链式：{"pipeline": ["<agent1>", "<agent2>"], "reason": "<简短原因>"}
不要返回任何其他内容。
"""),
                ChatMessage.User(userMessage)
            };

            var result = await llm.ChatAsync(turnContext, messages, null, ct);
            var content = result?.Content ?? "";

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("pipeline", out var pipelineProp) && pipelineProp.ValueKind == JsonValueKind.Array)
            {
                var pipeline = pipelineProp.EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => !string.IsNullOrEmpty(x) && AgentNameMap.ContainsKey(x.ToLower()))
                    .Select(x => AgentNameMap[x.ToLower()])
                    .Distinct()
                    .ToList();

                if (pipeline.Count > 1)
                {
                    var reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
                    return new RouteResult
                    {
                        AgentName = pipeline[0],
                        ContentType = GetContentType(pipeline[0]),
                        Reason = $"LLM意图分类(链式): {reason}",
                        Pipeline = pipeline
                    };
                }
            }

            if (root.TryGetProperty("agent", out var a))
            {
                var agentRaw = a.GetString() ?? "write";
                var reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";

                if (AgentNameMap.TryGetValue(agentRaw.ToLower(), out var mapped))
                {
                    return new RouteResult
                    {
                        AgentName = mapped,
                        ContentType = GetContentType(mapped),
                        Reason = $"LLM意图分类: {reason}"
                    };
                }
            }

            return keywordResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM意图分类失败，回退到关键词路由");
            return keywordResult;
        }
    }

    private static string GetContentType(string agentName) => agentName switch
    {
        "write" => "chapter",
        "outline" => "outline",
        "world" => "setting",
        "creation" => "character",
        "audit" => "audit_report",
        _ => "plain"
    };
}

public sealed class RouteResult
{
    public string AgentName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public List<string> Pipeline { get; set; } = new();
}
