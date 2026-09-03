using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story;

public sealed class CharacterGrowthProposalEntity : AggregateRootEntity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string WorkId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string SourceRunId { get; set; } = string.Empty;
    public string ProposalJson { get; set; } = string.Empty;
    public string Severity { get; set; } = "normal";
    public string Status { get; set; } = "needs_review";
    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
}
