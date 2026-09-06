namespace SpeakEase.AI.Lib.Runtime;

public sealed class RuntimePlanRequest
{
    public RunContext Context { get; init; } = new();
    public IReadOnlyList<RuntimePlanStep> Steps { get; init; } = Array.Empty<RuntimePlanStep>();
    public bool PublishEvents { get; init; } = true;
}

public sealed class RuntimePlanStep
{
    public string Id { get; init; } = string.Empty;
    public IReadOnlyList<string> DependsOn { get; init; } = Array.Empty<string>();
    public string ContentType { get; init; } = "plain";
    public Func<IReadOnlyDictionary<string, RuntimeArtifact>, RuntimeRunRequest> CreateRequest { get; init; }
}
