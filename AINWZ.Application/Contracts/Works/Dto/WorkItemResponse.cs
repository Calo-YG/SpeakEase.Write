namespace SpeakEase.Write.Application.Contracts.Works.Dto;

/// <summary>
/// 作品列表项响应 DTO。
/// </summary>
public sealed class WorkItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public List<string> StyleTags { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public int TotalWordCount { get; set; }
    public int ChapterCount { get; set; }
    public int VolumeCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Perspective { get; set; } = "third";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
