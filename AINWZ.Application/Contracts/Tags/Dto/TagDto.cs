namespace SpeakEase.Write.Application.Contracts.Tags.Dto;

/// <summary>
/// 标签响应 DTO。
/// </summary>
public sealed class TagItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

/// <summary>
/// 创建/更新标签请求 DTO。
/// </summary>
public sealed class SaveTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; }
    public string Color { get; set; }
}
