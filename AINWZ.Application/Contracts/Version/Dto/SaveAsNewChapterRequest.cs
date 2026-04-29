namespace SpeakEase.Write.Application.Contracts.Version.Dto;

public sealed class SaveAsNewChapterRequest
{
    public string ChapterId { get; set; } = string.Empty;
    public string SourceVersionId { get; set; } = string.Empty;
    public string NewTitle { get; set; } = string.Empty;
}
