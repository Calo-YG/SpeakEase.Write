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

public sealed class AgentPlanCandidateStep
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

/// <summary>
/// 将意图解析结果编译为经过校验的执行计划，支持受约束 DAG。
/// </summary>
public sealed class PlanCompiler
{
    private const int MaxSteps = 16;

    public AgentPlan Compile(IntentResolution intent, IReadOnlyCollection<string> registeredAgents)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(registeredAgents);

        var registered = new HashSet<string>(registeredAgents, StringComparer.OrdinalIgnoreCase);
        if (intent.PlanSteps is { Count: > 0 })
            return CompileDag(intent.PlanSteps, registered);

        var pipeline = intent.ExplicitSequence is { Count: > 0 }
            ? intent.ExplicitSequence
            : new[] { intent.PrimaryAgent };
        return new PlanResolver().Resolve(pipeline, registered);
    }

    private static AgentPlan CompileDag(
        IReadOnlyList<AgentPlanCandidateStep> candidates,
        HashSet<string> registered)
    {
        if (candidates.Count == 0 || candidates.Count > MaxSteps)
            throw new InvalidOperationException("Agent plan must contain between 1 and 16 steps.");

        var normalized = candidates.Select((candidate, index) => new CandidateNode
        {
            Index = index,
            Id = candidate.Id?.Trim() ?? string.Empty,
            AgentName = candidate.AgentName?.Trim().ToLowerInvariant() ?? string.Empty,
            DependsOn = (candidate.DependsOn ?? Array.Empty<string>())
                .Select(x => x?.Trim() ?? string.Empty)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        }).ToList();

        if (normalized.Any(x => x.Id.Length == 0) ||
            normalized.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Count)
            throw new InvalidOperationException("Agent plan step ids must be unique and non-empty.");

        if (normalized.Any(x => !registered.Contains(x.AgentName)))
            throw new InvalidOperationException("Agent plan contains an unregistered agent.");

        var byId = normalized.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        if (normalized.Any(x => x.DependsOn.Any(dependency =>
                !byId.ContainsKey(dependency) || dependency.Equals(x.Id, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Agent plan contains an unknown or self dependency.");

        var indegree = normalized.ToDictionary(x => x.Id, x => x.DependsOn.Count, StringComparer.OrdinalIgnoreCase);
        var dependents = normalized.ToDictionary(x => x.Id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var node in normalized)
        {
            foreach (var dependency in node.DependsOn)
                dependents[dependency].Add(node.Id);
        }

        var ready = normalized
            .Where(x => indegree[x.Id] == 0)
            .OrderBy(x => x.Index)
            .Select(x => x.Id)
            .ToList();
        var ordered = new List<AgentPlanStep>(normalized.Count);
        while (ready.Count > 0)
        {
            var id = ready[0];
            ready.RemoveAt(0);
            var node = byId[id];
            ordered.Add(new AgentPlanStep
            {
                Id = node.Id,
                AgentName = node.AgentName,
                DependsOn = node.DependsOn.ToArray()
            });

            foreach (var dependent in dependents[id].OrderBy(x => byId[x].Index))
            {
                indegree[dependent]--;
                if (indegree[dependent] == 0)
                    ready.Add(dependent);
            }

            ready = ready.OrderBy(x => byId[x].Index).ToList();
        }

        if (ordered.Count != normalized.Count)
            throw new InvalidOperationException("Agent plan contains a dependency cycle.");

        return new AgentPlan { Steps = ordered };
    }

    private sealed class CandidateNode
    {
        public int Index { get; init; }
        public string Id { get; init; } = string.Empty;
        public string AgentName { get; init; } = string.Empty;
        public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    }
}
