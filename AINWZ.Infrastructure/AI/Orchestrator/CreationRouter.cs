using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class CreationRouter(IServiceScopeFactory scopeFactory, ILogger<CreationRouter> logger)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<CreationRouter> _logger = logger;

    public async Task<RouteResult> DecideWithLLMAsync(string userMessage, IEnumerable<INovelAgent> agents, CancellationToken ct = default)
    {
        var agentsList = agents.ToList();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var llmContext = scope.ServiceProvider.GetRequiredService<IOpenAIContext>();
            await llmContext.ResolveAsync(ct);

            var llm = scope.ServiceProvider.GetRequiredService<IChatCompatible>();
            var turnContext = new LLMTurnContext { Model = llmContext.Model, Temperature = 0.1 };
            var systemPrompt = $$"""
你是一个意图分类器，负责将用户输入精准分类到以下 Agent。根据用户的真实意图和工作性质做出判断：

## Agent 职责说明

- **general**：通用问答助手。处理非写作类问题：闲聊、知识问答、实时信息查询（有网络搜索能力）。当用户的问题不属于以下任何写作范畴时，默认归入此 Agent。
  典型场景：问我今天天气/XX新闻/XX知识/帮我查资料/这个怎么做/闲聊

- **write**：小说正文写作 Agent。负责章节正文的写作、续写、润色、扩写和重写。管理伏笔埋设与回收、时间线维护、角色关系更新、写作规则读取。
  典型场景：帮我写第X章/续写一下/润色这段/扩写/重写/改写/写一节

- **world**：世界观架构 Agent。负责设计和管理世界观六维架构：世界规则、力量体系（修仙/武道/魔法等）、天道法则、地理与文明、势力格局（宗门/国家/组织）、世界历史。管理所有世界观要素的创建、查询和维护。
  典型场景：设计世界观/创建势力/添加地理/设定力量体系/这个世界有什么法则/加个宗门

- **outline**：故事大纲 Agent。负责全书总纲规划、卷结构设计、章节骨架创建、情节节点管理、高潮转折点布局。遵循自上而下（总纲→卷→章）的规划原则。
  典型场景：规划大纲/设计情节/规划第X卷/创建章节大纲/安排高潮/设计转折

- **creation**：角色设计 Agent。负责角色创建、角色信息更新、人物关系建立、角色成长线（角色弧）规划。也负责创意灵感生成。
  典型场景：创建角色/设计一个人物/给XX加个关系/规划角色成长/你的名字/角色有什么用/这个角色怎么出场

- **critique**：文风审查 Agent。专门检查已写文本的"AI味"，逐段审查用词、句式、心理描写、对话、环境描写是否符合真人写作风格，给出具体的修改方向。
  典型场景：检查一下文风/去AI味/看看这段像不像人写的/审查这篇文章/帮我检查一下

## 分类规则
1. 优先匹配最符合用户核心意图的 Agent（不是关键词匹配，而是意图匹配）
2. 如果用户同时提出多个意图（如"帮我写完这章然后检查一致性"），用 pipeline 按执行顺序排列
3. "文风"/"去AI味"/"审查"/"检查AI" → critique；"写"/"续写"/"润色"/"扩写" → write，两者同时出现 → pipeline: ["write", "critique"]
4. 不确定或超出上述所有范畴的 → general

返回 JSON 对象，格式：
单一意图：{"agent": "<agent_name>", "reason": "<简短原因>"}
多意图链式：{"pipeline": ["<agent1>", "<agent2>"], "reason": "<简短原因>"}
不要返回任何其他内容。
""";

            var messages = new List<ChatMessage>
            {
                ChatMessage.System(systemPrompt),
                ChatMessage.User(userMessage)
            };

            var result = await llm.ChatAsync(turnContext, messages, null, ct);
            var content = result?.Content ?? "";

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("pipeline", out var pipelineProp) && pipelineProp.ValueKind == JsonValueKind.Array)
            {
                var names = pipelineProp.EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => x.ToLower())
                    .ToList();

                var pipeline = new List<string>();
                foreach (var n in names)
                {
                    var matched = agentsList.FirstOrDefault(a => a.Name == n);
                    if (matched != null && !pipeline.Contains(matched.Name))
                        pipeline.Add(matched.Name);
                }

                if (pipeline.Count > 1)
                {
                    var reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
                    var firstAgent = pipeline[0];
                    var contentType = agentsList.FirstOrDefault(a => a.Name == firstAgent)?.Metadata.ContentType ?? "plain";
                    return new RouteResult
                    {
                        AgentName = firstAgent,
                        ContentType = contentType,
                        Reason = $"LLM意图分类(链式): {reason}",
                        Pipeline = pipeline
                    };
                }
            }

            if (root.TryGetProperty("agent", out var a))
            {
                var agentRaw = (a.GetString() ?? "general").ToLower();
                var reason = root.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
                var matched = agentsList.FirstOrDefault(x => x.Name == agentRaw);

                if (matched != null)
                {
                    return new RouteResult
                    {
                        AgentName = matched.Name,
                        ContentType = matched.Metadata.ContentType,
                        Reason = $"LLM意图分类: {reason}"
                    };
                }
            }

            return new RouteResult
            {
                AgentName = "general",
                ContentType = agentsList.FirstOrDefault(x => x.Name == "general")?.Metadata.ContentType ?? "plain",
                Reason = "LLM分类未命中，默认general"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM意图分类失败，回退到关键词路由");

            return new RouteResult
            {
                AgentName = "general",
                ContentType = agentsList.FirstOrDefault(x => x.Name == "general")?.Metadata.ContentType ?? "plain",
                Reason = "LLM分类未命中，默认general"
            };
        }
    }
}

public sealed class RouteResult
{
    public string AgentName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> Pipeline { get; set; } = new();
}
