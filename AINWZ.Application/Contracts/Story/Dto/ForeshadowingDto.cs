namespace SpeakEase.Write.Application.Contracts.Story.Dto;

public sealed class ForeshadowingItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SetupChapterId { get; set; } = string.Empty;
    public string PayoffChapterId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int Importance { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveForeshadowingRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SetupChapterId { get; set; }
    public string PayoffChapterId { get; set; }
    public string Status { get; set; }
    public int? Importance { get; set; }
}
