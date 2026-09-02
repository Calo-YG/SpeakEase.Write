namespace SpeakEase.Write.Application.Abstractions.AI;

public interface IAgentRuntimeStore : IAgentRunStore
{
    Task SaveCheckpointAsync(
        AgentCheckpointDto checkpoint,
        CancellationToken cancellationToken = default);

    Task<AgentCheckpointDto> LoadCheckpointAsync(
        string runId,
        string stepId,
        CancellationToken cancellationToken = default);
}
