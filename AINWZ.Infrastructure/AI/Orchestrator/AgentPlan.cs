namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

/// <summary>
/// 经过校验的 Agent 执行计划。当前实现为线性 Plan，为后续 DAG 保留依赖字段。
/// </summary>
public sealed class AgentPlan
{
    public IReadOnlyList<AgentPlanStep> Steps { get; init; } = Array.Empty<AgentPlanStep>();
}

public sealed class AgentPlanStep
{
    public string Id { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
}

public sealed class AgentArtifact
{
    public string Id { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int EstimatedTokens { get; init; }
}

public sealed class PlanResolver
{
    private const int MaxSteps = 16;

    public AgentPlan Resolve(IEnumerable<string> pipeline, IReadOnlyCollection<string> registeredAgents)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(registeredAgents);

        var names = pipeline
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .ToList();

        if (names.Count == 0 || names.Count > MaxSteps)
            throw new InvalidOperationException("Agent plan must contain between 1 and 16 steps.");

        var registered = new HashSet<string>(registeredAgents, StringComparer.OrdinalIgnoreCase);
        if (names.Any(x => !registered.Contains(x)))
            throw new InvalidOperationException("Agent plan contains an unregistered agent.");

        var steps = new List<AgentPlanStep>(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            steps.Add(new AgentPlanStep
            {
                Id = $"step-{i + 1}",
                AgentName = names[i],
                DependsOn = i == 0 ? Array.Empty<string>() : new[] { $"step-{i}" }
            });
        }

        return new AgentPlan { Steps = steps };
    }
}
