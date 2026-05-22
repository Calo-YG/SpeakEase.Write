using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

// 伏笔管理应用服务：管理伏笔的 CRUD、状态流转和待回收伏笔查询
public class ForeshadowingApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<ForeshadowingApplication> logger) : IForeshadowingApplication
{
    // 合法的伏笔状态枚举（pending 待回收 / resolved 已回收 / abandoned 放弃）
    private static readonly HashSet<string> AllowedStatuses = new() { "pending", "resolved", "abandoned" };

    // 验证作品归属权
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    // 验证章节是否存在且属于指定作品（空章节 ID 视为跳过校验）
    private async Task<bool> ChapterExistsAsync(string chapterId, string workId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chapterId)) return true;
        return await dbContext.Chapters.AnyAsync(c => c.Id == chapterId && c.WorkId == workId, ct);
    }

    // 查询作品下所有伏笔，支持按状态过滤（onlyPending），按重要性降序排列
    public async Task<ApiResult<List<ForeshadowingItemResponse>>> ListForeshadowingsAsync(string workId, bool? onlyPending = null, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<ForeshadowingItemResponse>>("作品不存在或无权访问。", 404);

        var query = dbContext.Foreshadowings.AsNoTracking()
            .Where(x => x.WorkId == workId);

        if (onlyPending == true)
            query = query.Where(x => x.Status == "pending");

        var list = await query
            .OrderByDescending(x => x.Importance)
            .ThenBy(x => x.Title)
            .Select(x => new ForeshadowingItemResponse
            {
                Id = x.Id, WorkId = x.WorkId, Title = x.Title,
                Description = x.Description, SetupChapterId = x.SetupChapterId,
                PayoffChapterId = x.PayoffChapterId, Status = x.Status,
                Importance = x.Importance, CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<ForeshadowingItemResponse>>(list);
    }

    // 按 ID 获取单个伏笔详情
    public async Task<ApiResult<ForeshadowingItemResponse>> GetForeshadowingByIdAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<ForeshadowingItemResponse>("作品不存在或无权访问。", 404);

        var result = await dbContext.Foreshadowings.AsNoTracking()
            .Where(x => x.Id == id && x.WorkId == workId)
            .Select(x => new ForeshadowingItemResponse
            {
                Id = x.Id, WorkId = x.WorkId, Title = x.Title,
                Description = x.Description, SetupChapterId = x.SetupChapterId,
                PayoffChapterId = x.PayoffChapterId, Status = x.Status,
                Importance = x.Importance, CreatedAt = x.CreateAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
            return new ApiResult<ForeshadowingItemResponse>("伏笔不存在。", 404);

        return new ApiResult<ForeshadowingItemResponse>(result);
    }

    // 创建伏笔：校验章节存在性、状态合法性和 resolved 状态必须有回收章节
    public async Task<ApiResult<ForeshadowingItemResponse>> CreateForeshadowingAsync(string workId, SaveForeshadowingRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<ForeshadowingItemResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Title))
            return new ApiResult<ForeshadowingItemResponse>("伏笔标题不能为空。", 400);

        if (!string.IsNullOrEmpty(request.SetupChapterId) && !await ChapterExistsAsync(request.SetupChapterId, workId, cancellationToken))
            return new ApiResult<ForeshadowingItemResponse>("埋设章节不存在。", 400);

        if (!string.IsNullOrEmpty(request.PayoffChapterId) && !await ChapterExistsAsync(request.PayoffChapterId, workId, cancellationToken))
            return new ApiResult<ForeshadowingItemResponse>("回收章节不存在。", 400);

        var status = (request.Status ?? "pending").ToLowerInvariant();
        if (!AllowedStatuses.Contains(status))
            return new ApiResult<ForeshadowingItemResponse>("无效的伏笔状态。", 400);

        if (status == "resolved" && string.IsNullOrEmpty(request.PayoffChapterId))
            return new ApiResult<ForeshadowingItemResponse>("回收状态必须指定回收章节。", 400);

        var entity = new ForeshadowingEntity
        {
            Id = idGenerator.NextIdString(),
            WorkId = workId,
            OwnerId = userId,
            Title = request.Title.Trim(),
            Description = request.Description ?? string.Empty,
            SetupChapterId = request.SetupChapterId ?? string.Empty,
            PayoffChapterId = request.PayoffChapterId ?? string.Empty,
            Status = status,
            Importance = request.Importance ?? 1,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.Foreshadowings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 在作品 {WorkId} 创建伏笔：{Title}，Status={Status}，Importance={Importance}",
            userId, workId, entity.Title, entity.Status, entity.Importance);

        return new ApiResult<ForeshadowingItemResponse>(new ForeshadowingItemResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Title = entity.Title,
            Description = entity.Description, SetupChapterId = entity.SetupChapterId,
            PayoffChapterId = entity.PayoffChapterId, Status = entity.Status,
            Importance = entity.Importance, CreatedAt = entity.CreateAt
        });
    }

    // 更新伏笔：支持部分字段更新（仅更新非 null 字段），含状态合法性校验
    public async Task<ApiResult<ForeshadowingItemResponse>> UpdateForeshadowingAsync(string workId, string id, SaveForeshadowingRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<ForeshadowingItemResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.Foreshadowings
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult<ForeshadowingItemResponse>("伏笔不存在。", 404);

        if (!string.IsNullOrEmpty(request.SetupChapterId) && !await ChapterExistsAsync(request.SetupChapterId, workId, cancellationToken))
            return new ApiResult<ForeshadowingItemResponse>("埋设章节不存在。", 400);

        if (!string.IsNullOrEmpty(request.PayoffChapterId) && !await ChapterExistsAsync(request.PayoffChapterId, workId, cancellationToken))
            return new ApiResult<ForeshadowingItemResponse>("回收章节不存在。", 400);

        if (request.Status is not null)
        {
            var newStatus = request.Status.ToLowerInvariant();
            if (!AllowedStatuses.Contains(newStatus))
                return new ApiResult<ForeshadowingItemResponse>("无效的伏笔状态。", 400);
            if (newStatus == "resolved" && string.IsNullOrEmpty(request.PayoffChapterId ?? entity.PayoffChapterId))
                return new ApiResult<ForeshadowingItemResponse>("回收状态必须指定回收章节。", 400);
            entity.Status = newStatus;
        }

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return new ApiResult<ForeshadowingItemResponse>("伏笔标题不能为空。", 400);
            entity.Title = request.Title.Trim();
        }
        if (request.Description is not null) entity.Description = request.Description;
        if (request.SetupChapterId is not null) entity.SetupChapterId = request.SetupChapterId;
        if (request.PayoffChapterId is not null) entity.PayoffChapterId = request.PayoffChapterId;
        if (request.Importance.HasValue) entity.Importance = request.Importance.Value;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<ForeshadowingItemResponse>(new ForeshadowingItemResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Title = entity.Title,
            Description = entity.Description, SetupChapterId = entity.SetupChapterId,
            PayoffChapterId = entity.PayoffChapterId, Status = entity.Status,
            Importance = entity.Importance, CreatedAt = entity.CreateAt
        });
    }

    // 删除伏笔（物理删除）
    public async Task<ApiResult> DeleteForeshadowingAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.Foreshadowings
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult("伏笔不存在。", 404);

        dbContext.Foreshadowings.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 删除伏笔：{Title}，Id={Id}", userId, entity.Title, entity.Id);

        return new ApiResult(true);
    }

    // 查询待回收伏笔：状态为 pending 且已有埋设章节的伏笔
    public async Task<ApiResult<List<ForeshadowingItemResponse>>> ListPendingResolutionsAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<ForeshadowingItemResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.Foreshadowings.AsNoTracking()
            .Where(x => x.WorkId == workId && x.Status == "pending" && !string.IsNullOrEmpty(x.SetupChapterId))
            .OrderBy(x => x.Importance)
            .Select(x => new ForeshadowingItemResponse
            {
                Id = x.Id, WorkId = x.WorkId, Title = x.Title,
                Description = x.Description, SetupChapterId = x.SetupChapterId,
                PayoffChapterId = x.PayoffChapterId, Status = x.Status,
                Importance = x.Importance, CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<ForeshadowingItemResponse>>(list);
    }
}
