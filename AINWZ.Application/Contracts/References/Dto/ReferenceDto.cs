using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Contracts.References.Dto;

/// <summary>
/// 参考作品响应 DTO。
/// </summary>
public sealed class ReferenceWorkItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public List<string> StyleTags { get; set; } = new();
    public decimal Score { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// 参考段落响应 DTO。
/// </summary>
public sealed class ReferencePassageItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string ReferenceWorkId { get; set; } = string.Empty;
    public string ReferenceWorkTitle { get; set; } = string.Empty;
    public string ReferenceWorkAuthor { get; set; } = string.Empty;
    public string ReferenceWorkGenre { get; set; } = string.Empty;
    public string PassageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> HighlightTags { get; set; } = new();
    public string TechniqueAnalysis { get; set; } = string.Empty;
    public int FavoriteCount { get; set; }
    public int RecommendationCount { get; set; }
    public bool FavoritedByMe { get; set; }
}

/// <summary>
/// 查询参考作品请求 DTO。
/// </summary>
public sealed class ReferenceWorkQueryRequest
{
    public string Keyword { get; set; }
}

/// <summary>
/// 查询参考段落请求 DTO。
/// </summary>
public sealed class ReferencePassageQueryRequest
{
    public string Keyword { get; set; }
    public string PassageType { get; set; }
    public string Tag { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 新增参考段落请求 DTO。
/// </summary>
public sealed class SaveReferencePassageRequest
{
    public string ReferenceWorkId { get; set; }
    public string BookTitle { get; set; }
    public string Author { get; set; }
    public string Genre { get; set; }
    public string PassageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> HighlightTags { get; set; }
    public string TechniqueAnalysis { get; set; }
}
