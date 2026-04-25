namespace SpeakEase.Write.Application.Contracts.Works.Dto;

/// <summary>
/// 创建作品请求 DTO。
/// </summary>
public sealed class CreateWorkRequest
{
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public List<string> StyleTags { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public string CoverUrl { get; set; }
}

/// <summary>
/// 更新作品请求 DTO。
/// </summary>
public sealed class UpdateWorkRequest
{
    public string Title { get; set; }
    public string Genre { get; set; }
    public List<string> StyleTags { get; set; }
    public string Description { get; set; }
    public string CoverUrl { get; set; }
    public string Status { get; set; }
}
