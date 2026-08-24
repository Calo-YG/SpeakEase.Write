using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.AI;

public sealed class AgentArtifactEntity : AggregateRootEntity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string RunId { get; set; } = string.Empty;
    public string StepId { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int EstimatedTokens { get; set; }
}
