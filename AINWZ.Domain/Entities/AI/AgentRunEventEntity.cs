using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.AI;

public sealed class AgentRunEventEntity : AggregateRootEntity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string RunId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}
