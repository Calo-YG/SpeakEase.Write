using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story;

public sealed class CharacterStateEventEntity : Entity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string WorkId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string SourceRunId { get; set; } = string.Empty;
    public string SourceChapterId { get; set; } = string.Empty;
    public string SourceEventKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = string.Empty;
    public string ChangesJson { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public long Version { get; set; }
}
