using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Domain.Entities.Learning;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEaseDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Applications;

// 灵感管理应用服务：管理作品关联的灵感/想法记录，支持增删改查和归档
public class InspirationApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<InspirationApplication> logger) : IInspirationApplication
{
    // 校验用户是否为作品的拥有者
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    // 列出作品下当前用户的所有灵感，按创建时间倒序排列
    public async Task<ApiResult<List<InspirationRecordResponse>>> ListInspirationsAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<InspirationRecordResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.InspirationRecords.AsNoTracking()
            .Where(x => x.WorkId == workId && x.UserId == userId)
            .OrderByDescending(x => x.CreateAt)
            .Select(x => new InspirationRecordResponse
            {
                Id = x.Id,
                WorkId = x.WorkId,
                InspirationType = x.InspirationType,
                Title = x.Title,
                Content = x.Content,
                Source = x.Source,
                IsArchived = x.IsArchived,
                CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<InspirationRecordResponse>>(list);
    }

    // 创建灵感：默认类型为idea，归档状态为false
    public async Task<ApiResult<InspirationRecordResponse>> CreateInspirationAsync(string workId, SaveInspirationRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<InspirationRecordResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Title))
            return new ApiResult<InspirationRecordResponse>("灵感标题不能为空。", 400);

        var entity = new InspirationRecordEntity
        {
            Id = idGenerator.NextIdString(),
            UserId = userId,
            WorkId = workId,
            InspirationType = request.InspirationType ?? "idea",
            Title = request.Title.Trim(),
            Content = request.Content ?? string.Empty,
            Source = request.Source ?? string.Empty,
            IsArchived = false,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.InspirationRecords.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 在作品 {WorkId} 创建灵感：{Title}，Type={Type}",
            userId, workId, entity.Title, entity.InspirationType);

        return new ApiResult<InspirationRecordResponse>(new InspirationRecordResponse
        {
            Id = entity.Id,
            WorkId = entity.WorkId,
            InspirationType = entity.InspirationType,
            Title = entity.Title,
            Content = entity.Content,
            Source = entity.Source,
            IsArchived = entity.IsArchived,
            CreatedAt = entity.CreateAt
        });
    }

    // 更新灵感：部分字段更新，标题为空时拒绝
    public async Task<ApiResult<InspirationRecordResponse>> UpdateInspirationAsync(string workId, string id, SaveInspirationRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<InspirationRecordResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.InspirationRecords
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId && x.UserId == userId, cancellationToken);
        if (entity is null)
            return new ApiResult<InspirationRecordResponse>("灵感记录不存在。", 404);

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return new ApiResult<InspirationRecordResponse>("灵感标题不能为空。", 400);
            entity.Title = request.Title.Trim();
        }
        if (request.Content is not null) entity.Content = request.Content;
        if (request.InspirationType is not null) entity.InspirationType = request.InspirationType;
        if (request.Source is not null) entity.Source = request.Source;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<InspirationRecordResponse>(new InspirationRecordResponse
        {
            Id = entity.Id,
            WorkId = entity.WorkId,
            InspirationType = entity.InspirationType,
            Title = entity.Title,
            Content = entity.Content,
            Source = entity.Source,
            IsArchived = entity.IsArchived,
            CreatedAt = entity.CreateAt
        });
    }

    // 删除灵感记录
    public async Task<ApiResult> DeleteInspirationAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.InspirationRecords
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId && x.UserId == userId, cancellationToken);
        if (entity is null)
            return new ApiResult("灵感记录不存在。", 404);

        dbContext.InspirationRecords.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult(true);
    }

    // 设置灵感的归档状态（true=归档隐藏, false=取消归档）
    public async Task<ApiResult> ArchiveInspirationAsync(string workId, string id, ArchiveInspirationRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.InspirationRecords
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId && x.UserId == userId, cancellationToken);
        if (entity is null)
            return new ApiResult("灵感记录不存在。", 404);

        entity.IsArchived = request.IsArchived;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 将灵感 {Title} 归档状态设为 {IsArchived}", userId, entity.Title, entity.IsArchived);

        return new ApiResult(true);
    }
}
