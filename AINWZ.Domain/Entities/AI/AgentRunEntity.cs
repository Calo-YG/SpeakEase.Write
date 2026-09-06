using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.AI;

public sealed class AgentRunEntity : AggregateRootEntity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string WorkId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string DeduplicationKey { get; set; } = string.Empty;
    public string ClientMessageId { get; set; } = string.Empty;
    public string Status { get; set; } = "running";
    public string StopReason { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TurnNumber { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
}
