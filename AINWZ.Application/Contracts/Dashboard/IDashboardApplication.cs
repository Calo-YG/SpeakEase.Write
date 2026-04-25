using SpeakEase.Write.Application.Contracts.Dashboard.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Dashboard;

/// <summary>
/// 仪表板应用服务接口。
/// </summary>
public interface IDashboardApplication
{
    Task<ApiResult<DashboardStatsResponse>> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<List<RecentWorkItemResponse>>> GetRecentWorksAsync(int limit, CancellationToken cancellationToken = default);
}
