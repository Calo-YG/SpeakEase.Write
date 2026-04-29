namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

/// <summary>
/// 创作路由决策
/// </summary>
public sealed class CreationRouter
{
    private static readonly Dictionary<string, (string Agent, string ContentType)> KeywordRules = new()
    {
        { "写",      ("write",    "chapter") },
        { "续写",    ("write",    "chapter") },
        { "润色",    ("write",    "chapter") },
        { "大纲",    ("outline",  "outline") },
        { "情节",    ("outline",  "outline") },
        { "规划",    ("outline",  "outline") },
        { "世界观",  ("world",    "setting") },
        { "设定",    ("world",    "setting") },
        { "世界",    ("world",    "setting") },
        { "角色",    ("creation", "character") },
        { "人物",    ("creation", "character") },
        { "创意",    ("creation", "plain") },
        { "点子",    ("creation", "plain") },
        { "脑洞",    ("creation", "plain") },
        { "检查",    ("audit",    "audit_report") },
        { "审阅",    ("audit",    "audit_report") },
        { "审核",    ("audit",    "audit_report") },
    };

    public RouteResult Decide(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return new RouteResult
            {
                AgentName = "write",
                ContentType = "plain",
                Reason = "空输入，默认路由到写作Agent"
            };

        foreach (var kv in KeywordRules)
        {
            if (userMessage.Contains(kv.Key))
            {
                return new RouteResult
                {
                    AgentName = kv.Value.Agent,
                    ContentType = kv.Value.ContentType,
                    Reason = $"关键词「{kv.Key}」匹配 → {kv.Value.Agent}"
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
}

public sealed class RouteResult
{
    public string AgentName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public List<string> Pipeline { get; set; } = new();
}
