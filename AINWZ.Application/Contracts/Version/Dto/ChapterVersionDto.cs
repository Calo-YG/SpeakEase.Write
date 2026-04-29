namespace SpeakEase.Write.Application.Contracts.Version.Dto;

public sealed class CreateVersionRequest
{
    public string ChapterId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Source { get; set; } = "manual";
    public string ModelId { get; set; } = string.Empty;
}

public sealed class ChapterVersionDto
{
    public string VersionId { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class ChapterVersionDetailDto
{
    public string VersionId { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
