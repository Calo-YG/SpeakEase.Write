using System.Text.Json;

using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

/// <summary>
/// 只负责理解用户目标并给出候选 Agent，不承担执行计划和运行策略。
/// </summary>
public sealed class IntentResolver
{
    public async Task<IntentResolution> ResolveAsync(
        string userMessage,
        IEnumerable<INovelAgent> agents,
        IOpenAIContext llmContext,
        IChatCompatible llm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(llmContext);
        ArgumentNullException.ThrowIfNull(llm);

        var agentList = agents.ToList();
        if (agentList.Count == 0)
            throw new InvalidOperationException("No Agent is registered.");

        await llmContext.ResolveAsync(cancellationToken);
        var registry = string.Join("\n", agentList.Select(x => $"- {x.Name}: {x.RouteDescription}"));
        var systemPrompt = $$"""
你负责理解用户当前任务，并从已注册能力中选择最合适的 Agent。

已注册 Agent：
{{registry}}

判断原则：
- 基于用户的整体目标和期望结果选择，不机械匹配局部措辞。
- 默认选择一个最能完成任务的 Agent。
- 只有用户明确要求多个相互独立且有先后顺序的阶段时，才返回 sequence。
- 信息不足以可靠选择时设置 needsClarification=true，并给出一个简短问题。

只返回 JSON：
{"agent":"name","confidence":0.0,"goals":["goal"],"reason":"reason","needsClarification":false,"clarificationQuestion":"","sequence":[],"steps":[]}
""";
        var result = await llm.ChatAsync(
            new LLMTurnContext { Model = llmContext.Model, Temperature = 0.1 },
            new List<ChatMessage> { ChatMessage.System(systemPrompt), ChatMessage.User(userMessage ?? string.Empty) },
            null,
            cancellationToken);

        if (result is null || !result.Success || string.IsNullOrWhiteSpace(result.Content))
            return Fallback(agentList, "Intent model returned no usable result.");

        return Parse(result.Content, agentList);
    }

    private static IntentResolution Parse(string content, IReadOnlyList<INovelAgent> agents)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var registered = new HashSet<string>(agents.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
        var primary = ReadString(root, "agent").ToLowerInvariant();
        if (!registered.Contains(primary))
            primary = ResolveFallbackAgent(agents);

        var sequence = ReadStringArray(root, "sequence");
        if (sequence.Count == 0)
            sequence = ReadStringArray(root, "pipeline");
        sequence = sequence
            .Select(x => x.ToLowerInvariant())
            .Where(registered.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sequence.Count == 1)
            sequence.Clear();

        return new IntentResolution
        {
            PrimaryAgent = primary,
            Confidence = root.TryGetProperty("confidence", out var confidence) && confidence.TryGetDouble(out var value)
                ? Math.Clamp(value, 0, 1)
                : 0.5,
            Goals = ReadStringArray(root, "goals"),
            Reason = ReadString(root, "reason"),
            NeedsClarification = root.TryGetProperty("needsClarification", out var clarification) &&
                                 clarification.ValueKind == JsonValueKind.True,
            ClarificationQuestion = ReadString(root, "clarificationQuestion"),
            ExplicitSequence = sequence,
            PlanSteps = ReadPlanSteps(root)
        };
    }

    private static IntentResolution Fallback(IReadOnlyList<INovelAgent> agents, string reason)
    {
        return new IntentResolution
        {
            PrimaryAgent = ResolveFallbackAgent(agents),
            Confidence = 0,
            Reason = reason
        };
    }

    private static string ResolveFallbackAgent(IReadOnlyList<INovelAgent> agents)
    {
        return agents.FirstOrDefault(x => x.Name.Equals("general", StringComparison.OrdinalIgnoreCase))?.Name
               ?? agents[0].Name;
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static List<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static List<AgentPlanCandidateStep> ReadPlanSteps(JsonElement root)
    {
        if (!root.TryGetProperty("steps", out var value) || value.ValueKind != JsonValueKind.Array)
            return new List<AgentPlanCandidateStep>();

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.Object)
            .Select(x => new AgentPlanCandidateStep
            {
                Id = ReadString(x, "id"),
                AgentName = ReadString(x, "agent"),
                DependsOn = ReadStringArray(x, "dependsOn")
            })
            .ToList();
    }
}

public sealed class IntentResolution
{
    public string PrimaryAgent { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public IReadOnlyList<string> Goals { get; init; } = Array.Empty<string>();
    public string Reason { get; init; } = string.Empty;
    public bool NeedsClarification { get; init; }
    public string ClarificationQuestion { get; init; } = string.Empty;
    public IReadOnlyList<string> ExplicitSequence { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AgentPlanCandidateStep> PlanSteps { get; init; } = Array.Empty<AgentPlanCandidateStep>();
}
