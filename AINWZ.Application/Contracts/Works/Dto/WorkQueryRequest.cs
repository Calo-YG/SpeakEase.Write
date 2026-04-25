namespace SpeakEase.Write.Application.Contracts.Works.Dto;

/// <summary>
/// 作品列表查询请求 DTO。
/// </summary>
public sealed class WorkQueryRequest
{
    public string Keyword { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
