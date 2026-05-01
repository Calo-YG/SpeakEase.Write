using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Works;
using SpeakEase.Write.Application.Contracts.Works.Dto;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

/// <summary>
/// 作品管理应用服务实现。
/// </summary>
public class WorkApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<WorkApplication> logger) : IWorkApplication
{
    public async Task<ApiResult<PageResult<WorkItemResponse>>> QueryWorksAsync(WorkQueryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = dbContext.Works.AsNoTracking()
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            query = query.Where(x => x.Title.Contains(request.Keyword) || x.Summary.Contains(request.Keyword));
        }

        var total = await query.CountAsync(cancellationToken);

        var workIds = await query
            .OrderByDescending(x => x.UpdateAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var works = await query
            .Where(x => workIds.Contains(x.Id))
            .OrderByDescending(x => x.UpdateAt)
            .Select(x => new { x.Id, x.Title, x.Genre, x.StyleTags, x.Perspective, x.Summary, x.CoverUrl, x.TotalWordCount, x.Status, x.CreateAt, x.UpdateAt })
            .ToListAsync(cancellationToken);

        // 批量查询章节数和卷数
        var chapterCounts = await dbContext.Chapters.AsNoTracking()
            .Where(x => workIds.Contains(x.WorkId))
            .GroupBy(x => x.WorkId)
            .Select(g => new { WorkId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WorkId, x => x.Count, cancellationToken);

        var volumeCounts = await dbContext.Volumes.AsNoTracking()
            .Where(x => workIds.Contains(x.WorkId))
            .GroupBy(x => x.WorkId)
            .Select(g => new { WorkId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WorkId, x => x.Count, cancellationToken);

        var items = works.Select(w => new WorkItemResponse
        {
            Id = w.Id,
            Title = w.Title,
            Genre = w.Genre,
            StyleTags = w.StyleTags,
            Perspective = w.Perspective,
            Description = w.Summary,
            CoverUrl = w.CoverUrl,
            TotalWordCount = w.TotalWordCount,
            ChapterCount = chapterCounts.GetValueOrDefault(w.Id, 0),
            VolumeCount = volumeCounts.GetValueOrDefault(w.Id, 0),
            Status = w.Status,
            CreatedAt = w.CreateAt,
            UpdatedAt = w.UpdateAt
        }).ToList();

        return new ApiResult<PageResult<WorkItemResponse>>(
            PageResult<WorkItemResponse>.Create(total, items, pageIndex, pageSize));
    }

    public async Task<ApiResult<WorkItemResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var work = await dbContext.Works.AsNoTracking()
            .Where(x => x.Id == id && x.UserId == userId)
            .Select(x => new { x.Id, x.Title, x.Genre, x.StyleTags, x.Perspective, x.Summary, x.CoverUrl, x.TotalWordCount, x.Status, x.CreateAt, x.UpdateAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (work is null)
            return new ApiResult<WorkItemResponse>($"未找到标识为 {id} 的作品。", 404);

        var chapterCount = await dbContext.Chapters.CountAsync(x => x.WorkId == id, cancellationToken);
        var volumeCount = await dbContext.Volumes.CountAsync(x => x.WorkId == id, cancellationToken);

        return new ApiResult<WorkItemResponse>(new WorkItemResponse
        {
            Id = work.Id,
            Title = work.Title,
            Genre = work.Genre,
            StyleTags = work.StyleTags,
            Perspective = work.Perspective,
            Description = work.Summary,
            CoverUrl = work.CoverUrl,
            TotalWordCount = work.TotalWordCount,
            ChapterCount = chapterCount,
            VolumeCount = volumeCount,
            Status = work.Status,
            CreatedAt = work.CreateAt,
            UpdatedAt = work.UpdateAt
        });
    }

    public async Task<ApiResult<WorkItemResponse>> CreateWorkAsync(CreateWorkRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return new ApiResult<WorkItemResponse>("作品名称不能为空。", 400);

        var userId = userContext.UserId;
        var entity = new WorkEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = userId,
            Title = request.Title.Trim(),
            Genre = request.Genre ?? string.Empty,
            StyleTags = request.StyleTags ?? new(),
            Perspective = request.Perspective ?? "third",
            Summary = request.Description ?? string.Empty,
            CoverUrl = request.CoverUrl ?? string.Empty,
            Status = "draft",
            TotalWordCount = 0,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.Works.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 创建作品：{Title}，Id={Id}", userId, entity.Title, entity.Id);

        return new ApiResult<WorkItemResponse>(new WorkItemResponse
        {
            Id = entity.Id,
            Title = entity.Title,
            Genre = entity.Genre,
            StyleTags = entity.StyleTags,
            Perspective = entity.Perspective,
            Description = entity.Summary,
            CoverUrl = entity.CoverUrl,
            TotalWordCount = 0,
            ChapterCount = 0,
            VolumeCount = 0,
            Status = entity.Status,
            CreatedAt = entity.CreateAt,
            UpdatedAt = entity.UpdateAt
        });
    }

    public async Task<ApiResult<WorkItemResponse>> UpdateWorkAsync(string id, UpdateWorkRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var entity = await dbContext.Works
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (entity is null)
            return new ApiResult<WorkItemResponse>($"未找到标识为 {id} 的作品。", 404);

        if (request.Title is not null) entity.Title = request.Title.Trim();
        if (request.Genre is not null) entity.Genre = request.Genre;
        if (request.StyleTags is not null) entity.StyleTags = request.StyleTags;
        if (request.Perspective is not null) entity.Perspective = request.Perspective;
        if (request.Description is not null) entity.Summary = request.Description;
        if (request.CoverUrl is not null) entity.CoverUrl = request.CoverUrl;
        if (request.Status is not null) entity.Status = request.Status;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var chapterCount = await dbContext.Chapters.CountAsync(x => x.WorkId == id, cancellationToken);
        var volumeCount = await dbContext.Volumes.CountAsync(x => x.WorkId == id, cancellationToken);

        return new ApiResult<WorkItemResponse>(new WorkItemResponse
        {
            Id = entity.Id,
            Title = entity.Title,
            Genre = entity.Genre,
            StyleTags = entity.StyleTags,
            Perspective = entity.Perspective,
            Description = entity.Summary,
            CoverUrl = entity.CoverUrl,
            TotalWordCount = entity.TotalWordCount,
            ChapterCount = chapterCount,
            VolumeCount = volumeCount,
            Status = entity.Status,
            CreatedAt = entity.CreateAt,
            UpdatedAt = entity.UpdateAt
        });
    }

    public async Task<ApiResult> DeleteWorkAsync(string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var entity = await dbContext.Works
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (entity is null)
            return new ApiResult($"未找到标识为 {id} 的作品。", 404);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var chapterIds = await dbContext.Chapters.Where(x => x.WorkId == id).Select(x => x.Id).ToListAsync(cancellationToken);
            if (chapterIds.Count > 0)
                await dbContext.ChapterVersions.Where(x => chapterIds.Contains(x.ChapterId)).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Chapters.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            await dbContext.Volumes.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            await dbContext.Characters.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterRelationships.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterArcs.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterGraphNodes.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterGraphEdges.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterGraphs.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            var outlineIds = await dbContext.Outlines.Where(x => x.WorkId == id).Select(x => x.Id).ToListAsync(cancellationToken);
            if (outlineIds.Count > 0)
                await dbContext.OutlineNodes.Where(x => outlineIds.Contains(x.OutlineId)).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Outlines.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            await dbContext.Foreshadowings.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.TimelineEvents.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.InspirationRecords.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            await dbContext.WorldRules.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.PowerSystems.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Factions.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Geographies.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.HistoricalEvents.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.WorldSettings.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            await dbContext.AICreationSessions.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.MemorySnapshots.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.ContextAssemblyLogs.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.AIGenerationTasks.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.ChapterAnalysisResults.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            dbContext.Works.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "删除作品 {Id} 事务失败，已回滚", id);
            return new ApiResult("删除作品失败，请稍后重试。", 500);
        }

        logger.LogInformation("用户 {UserId} 删除作品：{Title}，Id={Id}（含所有关联数据）", userId, entity.Title, entity.Id);

        return new ApiResult(true);
    }
}
