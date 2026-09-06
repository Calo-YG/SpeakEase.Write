namespace SpeakEase.AI.Lib.Runtime;

public sealed class LinearStepScheduler : IStepScheduler
{
    public IReadOnlyList<RuntimePlanStep> Order(IReadOnlyList<RuntimePlanStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
            throw new InvalidOperationException("Runtime plan must contain at least one step.");

        var indexed = steps.Select((step, index) => new { Step = step, Index = index }).ToArray();
        if (indexed.Any(x => x.Step is null || string.IsNullOrWhiteSpace(x.Step.Id) || x.Step.CreateRequest is null))
            throw new InvalidOperationException("Runtime plan steps require an id and request factory.");
        if (indexed.Select(x => x.Step.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != indexed.Length)
            throw new InvalidOperationException("Runtime plan step ids must be unique.");

        var byId = indexed.ToDictionary(x => x.Step.Id, StringComparer.OrdinalIgnoreCase);
        if (indexed.Any(x => x.Step.DependsOn.Any(dependency =>
                !byId.ContainsKey(dependency) || dependency.Equals(x.Step.Id, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Runtime plan contains an unknown or self dependency.");

        var indegree = indexed.ToDictionary(x => x.Step.Id, x => x.Step.DependsOn.Count, StringComparer.OrdinalIgnoreCase);
        var dependents = indexed.ToDictionary(x => x.Step.Id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var item in indexed)
        {
            foreach (var dependency in item.Step.DependsOn)
                dependents[dependency].Add(item.Step.Id);
        }

        var ready = indexed.Where(x => indegree[x.Step.Id] == 0).OrderBy(x => x.Index).ToList();
        var ordered = new List<RuntimePlanStep>(steps.Count);
        while (ready.Count > 0)
        {
            var current = ready[0];
            ready.RemoveAt(0);
            ordered.Add(current.Step);
            foreach (var dependent in dependents[current.Step.Id].OrderBy(x => byId[x].Index))
            {
                indegree[dependent]--;
                if (indegree[dependent] == 0)
                    ready.Add(byId[dependent]);
            }
            ready = ready.OrderBy(x => x.Index).ToList();
        }

        if (ordered.Count != steps.Count)
            throw new InvalidOperationException("Runtime plan contains a dependency cycle.");
        return ordered;
    }
}
