using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Memory;

public sealed class MemoryFactEntity : AggregateRootEntity, IOwner
{
    public string UserId { get; set; } = string.Empty;
    public string OwnerId => UserId;
    public string WorkId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SourceTurn { get; set; }
    public double Confidence { get; set; }
    public bool IsCurrent { get; set; } = true;
    public int VersionTurn { get; set; }
}
