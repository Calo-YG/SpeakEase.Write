namespace SpeakEase.Write.Application.Contracts.Creation.Dto;

public sealed class AdoptChapterRequest
{
    public string WorkId { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string AdoptType { get; set; } = "full";
}
