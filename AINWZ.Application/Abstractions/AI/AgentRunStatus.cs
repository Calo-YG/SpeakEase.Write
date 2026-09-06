namespace SpeakEase.Write.Application.Abstractions.AI;

public enum AgentRunStatus
{
    Completed,
    Failed,
    Cancelled,
    TimedOut,
    MaxIterationsReached,
    InvalidRequest
}
