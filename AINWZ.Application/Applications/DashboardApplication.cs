using Microsoft.EntityFrameworkCore;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Dashboard;
using SpeakEase.Write.Application.Contracts.Dashboard.Dto;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

/// <summary>
/// 仪表板应用服务实现。
/// </summary>
public class DashboardApplication(
    SpeakEaseDbContext dbContext,
    IUserContext userContext) : IDashboardApplication
{
    public async Task<ApiResult<DashboardStatsResponse>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;

        var works = await dbContext.Works.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.TotalWordCount, x.CreateAt })
            .ToListAsync(cancellationToken);

        var totalWords = works.Sum(x => x.TotalWordCount);
        var workCount = works.Count;

        // 创作天数：从最早的作品创建日期到今天
        var creationDays = works.Count > 0
            ? (int)(DateTime.UtcNow - works.Min(x => x.CreateAt)).TotalDays + 1
            : 0;

        var aiCallCount = await dbContext.LlmCallLogs.AsNoTracking()
            .Where(x => x.OwnerId == userId)
            .CountAsync(cancellationToken);

        return new ApiResult<DashboardStatsResponse>(new DashboardStatsResponse
        {
            TotalWords = totalWords,
            WorkCount = workCount,
            CreationDays = creationDays,
            AiCallCount = aiCallCount
        });
    }

    public async Task<ApiResult<List<RecentWorkItemResponse>>> GetRecentWorksAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 5;
        var userId = userContext.UserId;

        var list = await dbContext.Works.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdateAt)
            .Take(limit)
            .Select(x => new RecentWorkItemResponse
            {
                Id = x.Id,
                Title = x.Title,
                Genre = x.Genre,
                TotalWordCount = x.TotalWordCount,
                Status = x.Status,
                UpdatedAt = x.UpdateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<RecentWorkItemResponse>>(list);
    }
}
