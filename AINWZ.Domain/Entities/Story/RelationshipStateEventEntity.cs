using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story;

public sealed class RelationshipStateEventEntity : Entity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string WorkId { get; set; } = string.Empty;
    public string SourceCharacterId { get; set; } = string.Empty;
    public string TargetCharacterId { get; set; } = string.Empty;
    public string SourceRunId { get; set; } = string.Empty;
    public string SourceChapterId { get; set; } = string.Empty;
    public string ChangesJson { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public long Version { get; set; }
}
