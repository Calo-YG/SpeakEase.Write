using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Contracts.Works;
using SpeakEase.Write.Application.Contracts.Works.Dto;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEaseDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Applications;

/// <summary>
/// 作品管理应用服务实现。
/// </summary>
// 管理作品的 CRUD 操作，删除时在事务中级联清理所有关联数据
public class WorkApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<WorkApplication> logger) : IWorkApplication
{
    // 分页查询用户作品列表：先分页查 ID，再按 ID 批量查详情（避免 SELECT * 到内存再分页）
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

        var works = await query
            .OrderByDescending(x => x.UpdateAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Genre,
                x.StyleTags,
                x.Perspective,
                x.Summary,
                x.CoverUrl,
                x.TotalWordCount,
                x.Status,
                x.CreateAt,
                x.UpdateAt,
                ChapterCount = dbContext.Chapters.Count(chapter => chapter.WorkId == x.Id),
                VolumeCount = dbContext.Volumes.Count(volume => volume.WorkId == x.Id)
            })
            .ToListAsync(cancellationToken);

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
            ChapterCount = w.ChapterCount,
            VolumeCount = w.VolumeCount,
            Status = w.Status,
            CreatedAt = w.CreateAt,
            UpdatedAt = w.UpdateAt
        }).ToList();

        return new ApiResult<PageResult<WorkItemResponse>>(
            PageResult<WorkItemResponse>.Create(total, items, pageIndex, pageSize));
    }

    // 按 ID 获取作品详情，同时查询关联的章节数和卷数
    public async Task<ApiResult<WorkItemResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var work = await dbContext.Works.AsNoTracking()
            .Where(x => x.Id == id && x.UserId == userId)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Genre,
                x.StyleTags,
                x.Perspective,
                x.Summary,
                x.CoverUrl,
                x.TotalWordCount,
                x.Status,
                x.CreateAt,
                x.UpdateAt,
                ChapterCount = dbContext.Chapters.Count(chapter => chapter.WorkId == x.Id),
                VolumeCount = dbContext.Volumes.Count(volume => volume.WorkId == x.Id)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (work is null)
            return new ApiResult<WorkItemResponse>($"未找到标识为 {id} 的作品。", 404);

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
            ChapterCount = work.ChapterCount,
            VolumeCount = work.VolumeCount,
            Status = work.Status,
            CreatedAt = work.CreateAt,
            UpdatedAt = work.UpdateAt
        });
    }

    // 创建新作品：初始状态为 draft，总字数为 0
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

    // 更新作品信息：仅更新请求中非 null 的字段
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
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var counts = await dbContext.Works.AsNoTracking()
            .Where(x => x.Id == id && x.UserId == userId)
            .Select(x => new
            {
                ChapterCount = dbContext.Chapters.Count(chapter => chapter.WorkId == x.Id),
                VolumeCount = dbContext.Volumes.Count(volume => volume.WorkId == x.Id)
            })
            .FirstAsync(cancellationToken);

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
            ChapterCount = counts.ChapterCount,
            VolumeCount = counts.VolumeCount,
            Status = entity.Status,
            CreatedAt = entity.CreateAt,
            UpdatedAt = entity.UpdateAt
        });
    }

    // 删除作品及其所有关联数据：在事务中使用 ExecuteDeleteAsync 批量删除，避免加载到内存
    public async Task<ApiResult> DeleteWorkAsync(string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var entity = await dbContext.Works
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

        if (entity is null)
            return new ApiResult($"未找到标识为 {id} 的作品。", 404);

        // 事务：确保所有关联数据清理成功后统一提交，失败则全部回滚
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 先获取章节 ID 列表，用于级联删除章节版本
            var chapterIds = await dbContext.Chapters.Where(x => x.WorkId == id).Select(x => x.Id).ToListAsync(cancellationToken);
            if (chapterIds.Count > 0)
                await dbContext.ChapterVersions.Where(x => chapterIds.Contains(x.ChapterId)).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Chapters.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            await dbContext.Volumes.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            // 角色相关数据清理
            await dbContext.Characters.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterRelationships.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterArcs.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterGraphNodes.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterGraphEdges.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.CharacterGraphs.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            // 大纲数据：先获取大纲 ID 再批量删除大纲节点
            var outlineIds = await dbContext.Outlines.Where(x => x.WorkId == id).Select(x => x.Id).ToListAsync(cancellationToken);
            if (outlineIds.Count > 0)
                await dbContext.OutlineNodes.Where(x => outlineIds.Contains(x.OutlineId)).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Outlines.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            // 故事相关数据
            await dbContext.Foreshadowings.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.TimelineEvents.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.InspirationRecords.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            // 世界观相关数据
            await dbContext.WorldRules.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.PowerSystems.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Factions.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.Geographies.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.HistoricalEvents.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.WorldSettings.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            // AI 相关数据
            var sessionIds = await dbContext.AICreationSessions
                .Where(x => x.WorkId == id)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            if (sessionIds.Count > 0)
            {
                await dbContext.AICreationMessages
                    .Where(x => sessionIds.Contains(x.SessionId))
                    .ExecuteDeleteAsync(cancellationToken);
            }
            await dbContext.AICreationSessions.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            var agentRunIds = dbContext.AgentRuns
                .Where(x => x.WorkId == id)
                .Select(x => x.Id);
            await dbContext.AgentRunEvents
                .Where(x => agentRunIds.Contains(x.RunId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.AgentToolCalls
                .Where(x => agentRunIds.Contains(x.RunId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.AgentArtifacts
                .Where(x => agentRunIds.Contains(x.RunId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.AgentRuns.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.MemorySnapshots.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.MemoryFacts.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.ContextAssemblyLogs.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.AIGenerationTasks.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.ChapterAnalysisResults.Where(x => x.WorkId == id).ExecuteDeleteAsync(cancellationToken);

            // 最后删除作品本身
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
