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

    public RouteResult Decide(string userMessage, IEnumerable<INovelAgent> agents)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return new RouteResult { AgentName = "general", ContentType = "plain", Reason = "空输入" };

        var agentsList = agents.ToList();
        var bestMatch = default((string Agent, string ContentType, int KeywordLen)?);

        foreach (var agent in agentsList)
        {
            foreach (var kw in agent.Metadata.RouteKeywords)
            {
                if (!userMessage.Contains(kw.Keyword)) continue;
                if (!bestMatch.HasValue || kw.Keyword.Length > bestMatch.Value.KeywordLen)
                    bestMatch = (agent.Name, kw.ContentType, kw.Keyword.Length);
            }
        }

        if (bestMatch.HasValue)
        {
            var (agent, ct, _) = bestMatch.Value;
            return new RouteResult { AgentName = agent, ContentType = ct, Reason = $"关键词匹配 → {agent}" };
        }

        return new RouteResult { AgentName = "general", ContentType = "plain", Reason = "未匹配，默认通用助手" };
    }

    public async Task<RouteResult> DecideWithLLMAsync(string userMessage, IEnumerable<INovelAgent> agents, CancellationToken ct = default)
    {
        var agentsList = agents.ToList();
        var keywordResult = Decide(userMessage, agentsList);

        if (keywordResult.AgentName != "general" || userMessage.Length <= 15)
            return keywordResult;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var llmContext = scope.ServiceProvider.GetRequiredService<IOpenAIContext>();
            await llmContext.ResolveAsync(ct);

            var llm = scope.ServiceProvider.GetRequiredService<IChatCompatible>();
            var turnContext = new LLMTurnContext { Model = llmContext.Model, Temperature = 0.1 };

            var agentLines = new List<string>();
            foreach (var ag in agentsList)
                agentLines.Add($"- {ag.Name}: {ag.RouteDescription}");

            var systemPrompt = $$"""
你是一个意图分类器，负责将用户输入分类为以下 Agent：
{{string.Join("\n", agentLines)}}

用户可能包含多个意图（如"帮我写完这章然后检查一致性"），请识别并返回 pipeline。

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

            return keywordResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM意图分类失败，回退到关键词路由");
            return keywordResult;
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
