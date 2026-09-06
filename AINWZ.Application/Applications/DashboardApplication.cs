using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Contracts.Dashboard;
using SpeakEase.Write.Application.Contracts.Dashboard.Dto;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEaseDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Applications;

// 仪表板应用服务：聚合用户的创作统计数据（总字数、作品数、创作天数、AI调用次数）
public class DashboardApplication(
    SpeakEaseDbContext dbContext,
    IUserContext userContext) : IDashboardApplication
{
    // 获取仪表板统计数据：汇总当前用户的所有作品信息
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
            ? (int)(DateTime.Now - works.Min(x => x.CreateAt)).TotalDays + 1
            : 0;

        // 统计用户所有AI调用次数（不分类型）
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

    // 获取最近更新的作品列表，按更新时间倒序取前N条
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
