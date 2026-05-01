namespace SpeakEase.Write.Application.Contracts.Story.Dto;

/// <summary>
/// 章节列表项响应 DTO。
/// </summary>
public class ChapterItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string VolumeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int WordCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string AuthorNotes { get; set; } = string.Empty;
    public DateTime? LastContentSavedAt { get; set; }
}

/// <summary>
/// 章节详情响应 DTO（含正文内容）。
/// </summary>
public sealed class ChapterDetailResponse : ChapterItemResponse
{
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 创建章节请求 DTO。
/// </summary>
public sealed class CreateChapterRequest
{
    public string Title { get; set; } = string.Empty;
    public int? Sequence { get; set; }
}

/// <summary>
/// 更新章节请求 DTO。
/// </summary>
public sealed class UpdateChapterRequest
{
    public string Title { get; set; }
    public string Content { get; set; }
    public string Status { get; set; }
    public string AuthorNotes { get; set; }
}
