namespace SpeakEase.Write.Application.Abstractions.AI;

public sealed class AgentRunResult
{
    public AgentRunStatus Status { get; init; }
    public string StopReason { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;

    public bool IsSuccess => Status == AgentRunStatus.Completed;
}
