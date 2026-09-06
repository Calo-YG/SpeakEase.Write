using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story;

public sealed class CharacterStateSnapshotEntity : AggregateRootEntity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string WorkId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string BasedOnEventId { get; set; } = string.Empty;
    public string StateJson { get; set; } = string.Empty;
    public long Version { get; set; }
    public string Status { get; set; } = "confirmed";
}
