namespace SpeakEase.Write.Application.Contracts.Story.Dto;

public sealed class TimelineEventItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public string EventType { get; set; } = string.Empty;
    public List<string> RelatedCharacterIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveTimelineEventRequest
{
    public string ChapterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? EventTime { get; set; }
    public string EventType { get; set; }
    public List<string> RelatedCharacterIds { get; set; }
}
