namespace SpeakEase.Write.Application.Contracts.Dashboard.Dto;

/// <summary>
/// 仪表板统计数据 DTO。
/// </summary>
public sealed class DashboardStatsResponse
{
    public int TotalWords { get; set; }
    public int WorkCount { get; set; }
    public int CreationDays { get; set; }
    public int AiCallCount { get; set; }
}

/// <summary>
/// 最近作品列表项 DTO。
/// </summary>
public sealed class RecentWorkItemResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int TotalWordCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
