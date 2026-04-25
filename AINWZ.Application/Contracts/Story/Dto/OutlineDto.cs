namespace SpeakEase.Write.Application.Contracts.Story.Dto;

/// <summary>
/// 大纲节点响应 DTO。
/// </summary>
public sealed class OutlineNodeItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string WorkId { get; set; } = string.Empty;
    public string ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string ChapterId { get; set; }
}

/// <summary>
/// 创建/更新大纲节点请求 DTO。
/// </summary>
public sealed class SaveOutlineNodeRequest
{
    public string ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; }
    public int? Sequence { get; set; }
    public string ChapterId { get; set; }
}
