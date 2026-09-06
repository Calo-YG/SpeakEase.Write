using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.AI;

public sealed class AgentToolCallEntity : AggregateRootEntity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string RunId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string ToolCallId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsHash { get; set; } = string.Empty;
    public string Status { get; set; } = "started";
    public string ResultJson { get; set; } = string.Empty;
}
