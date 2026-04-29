namespace SpeakEase.Write.Application.Contracts.Story.Dto;

public sealed class AutoSaveRequest
{
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Content { get; set; }
    public string Title { get; set; }
    public string Summary { get; set; }
}
