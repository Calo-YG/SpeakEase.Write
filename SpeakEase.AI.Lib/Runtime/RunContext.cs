namespace SpeakEase.AI.Lib.Runtime;

public sealed class RunContext
{
    public string RunId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string WorkId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public AgentRuntimeOptions Options { get; init; } = new();
    public CancellationToken CancellationToken { get; init; }
}
