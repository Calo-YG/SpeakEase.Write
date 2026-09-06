namespace SpeakEase.Write.Application.Abstractions.AI;

public sealed class AgentCheckpointDto
{
    public string Id { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string MessagesJson { get; init; } = string.Empty;
    public int Iteration { get; init; }
    public string PendingToolCallsJson { get; init; } = string.Empty;
    public long Version { get; init; }
}
