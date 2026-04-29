namespace SpeakEase.Write.Application.Contracts.Snapshot.Dto;

public sealed class SnapshotSummaryDto
{
    public string SnapshotId { get; set; } = string.Empty;
    public string SnapshotType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string VersionId { get; set; } = string.Empty;
}
