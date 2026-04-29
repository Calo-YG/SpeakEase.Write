namespace SpeakEase.Write.Application.Contracts.Story.Dto;

public sealed class InspirationRecordResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string InspirationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveInspirationRequest
{
    public string InspirationType { get; set; } = "idea";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Source { get; set; }
}

public sealed class ArchiveInspirationRequest
{
    public bool IsArchived { get; set; } = true;
}
