using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.AI;

public sealed class AgentCheckpointEntity : AggregateRootEntity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string RunId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string MessagesJson { get; set; } = string.Empty;
    public int Iteration { get; set; }
    public string PendingToolCallsJson { get; set; } = string.Empty;
    public long Version { get; set; }
}
