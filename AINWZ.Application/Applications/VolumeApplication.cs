using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEaseDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Applications;

// 卷管理应用服务：管理作品的分卷结构，支持卷的增删改查、合并、章节移入/移出
public class VolumeApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<VolumeApplication> logger) : IVolumeApplication
{
    // 校验用户是否为作品的拥有者
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    // 列出作品下所有卷，按序号排序，同时统计每卷包含的章节数
    public async Task<ApiResult<List<VolumeItemResponse>>> ListVolumesAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<VolumeItemResponse>>("作品不存在或无权访问。", 404);

        var volumes = await dbContext.Volumes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Sequence)
            .Select(x => new VolumeItemResponse
            {
                Id = x.Id,
                WorkId = x.WorkId,
                Title = x.Title,
                Sequence = x.Sequence,
                Summary = x.Summary,
                ChapterCount = dbContext.Chapters.Count(c => c.VolumeId == x.Id)
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<VolumeItemResponse>>(volumes);
    }

    // 创建新卷：序号默认为当前最大序号+1，支持手动指定
    public async Task<ApiResult<VolumeItemResponse>> CreateVolumeAsync(string workId, CreateVolumeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<VolumeItemResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Title))
            return new ApiResult<VolumeItemResponse>("卷名称不能为空。", 400);

        var maxSeq = await dbContext.Volumes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .Select(x => (int?)x.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

        var entity = new VolumeEntity
        {
            Id = idGenerator.NextIdString(),
            WorkId = workId,
            OwnerId = userId,
            Title = request.Title.Trim(),
            Sequence = request.Sequence ?? maxSeq + 1,
            Summary = request.Summary ?? string.Empty,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.Volumes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 在作品 {WorkId} 创建卷：{Title}", userId, workId, entity.Title);

        return new ApiResult<VolumeItemResponse>(ToResponse(entity, 0));
    }

    // 更新卷：部分字段更新（标题、摘要、序号），返回当前章节数
    public async Task<ApiResult<VolumeItemResponse>> UpdateVolumeAsync(string workId, string volumeId, UpdateVolumeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<VolumeItemResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.Volumes
            .FirstOrDefaultAsync(x => x.Id == volumeId && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult<VolumeItemResponse>("卷不存在。", 404);

        if (request.Title is not null) entity.Title = request.Title.Trim();
        if (request.Summary is not null) entity.Summary = request.Summary;
        if (request.Sequence.HasValue) entity.Sequence = request.Sequence.Value;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var chapterCount = await dbContext.Chapters.CountAsync(c => c.VolumeId == volumeId, cancellationToken);

        return new ApiResult<VolumeItemResponse>(ToResponse(entity, chapterCount));
    }

    // 删除卷：使用ExecuteUpdateAsync批量解绑章节（不加载到内存），再删除卷实体
    public async Task<ApiResult> DeleteVolumeAsync(string workId, string volumeId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.Volumes
            .FirstOrDefaultAsync(x => x.Id == volumeId && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult("卷不存在。", 404);

        await dbContext.Chapters
            .Where(x => x.VolumeId == volumeId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.VolumeId, string.Empty), cancellationToken);

        dbContext.Volumes.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 删除卷：{Title}，Id={Id}，关联章节已解绑",
            userId, entity.Title, entity.Id);

        return new ApiResult(true);
    }

    // 合并卷：将源卷下所有章节移至目标卷末尾（重排序号），并删除源卷
    public async Task<ApiResult> MergeVolumesAsync(string workId, string volumeId, MergeVolumeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.TargetVolumeId))
            return new ApiResult("目标卷ID不能为空。", 400);
        if (volumeId == request.TargetVolumeId)
            return new ApiResult("不能合并自身。", 400);

        var source = await dbContext.Volumes
            .FirstOrDefaultAsync(x => x.Id == volumeId && x.WorkId == workId, cancellationToken);
        if (source is null)
            return new ApiResult("源卷不存在。", 404);

        var target = await dbContext.Volumes
            .FirstOrDefaultAsync(x => x.Id == request.TargetVolumeId && x.WorkId == workId, cancellationToken);
        if (target is null)
            return new ApiResult("目标卷不存在。", 404);

        // 获取目标卷当前最大序号，用于后续章节重新编号
        var maxSeqInTarget = await dbContext.Chapters.AsNoTracking()
            .Where(x => x.VolumeId == request.TargetVolumeId)
            .Select(x => (int?)x.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

        var sourceChapters = await dbContext.Chapters
            .Where(x => x.VolumeId == volumeId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(cancellationToken);

        // 将源卷章节逐个转移并重新编号
        foreach (var chapter in sourceChapters)
        {
            chapter.VolumeId = request.TargetVolumeId;
            chapter.Sequence = ++maxSeqInTarget;
        }

        dbContext.Volumes.Remove(source);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 将卷 {SourceTitle} 合并到卷 {TargetTitle}，转移 {Count} 个章节",
            userId, source.Title, target.Title, sourceChapters.Count);

        return new ApiResult(true);
    }

    // 将章节移入指定卷
    public async Task<ApiResult> MoveChapterAsync(string workId, string chapterId, string targetVolumeId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var chapter = await dbContext.Chapters
            .FirstOrDefaultAsync(x => x.Id == chapterId && x.WorkId == workId, cancellationToken);
        if (chapter is null)
            return new ApiResult("章节不存在。", 404);

        var targetVolume = await dbContext.Volumes
            .FirstOrDefaultAsync(x => x.Id == targetVolumeId && x.WorkId == workId, cancellationToken);
        if (targetVolume is null)
            return new ApiResult("目标卷不存在。", 404);

        if (chapter.VolumeId == targetVolumeId)
            return new ApiResult("章节已在目标卷中。", 400);

        chapter.VolumeId = targetVolumeId;
        chapter.UpdateBy = userId;
        chapter.UpdateAt = DateTime.Now;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 将章节 {ChapterId} 移入卷 {VolumeId}",
            userId, chapterId, targetVolumeId);

        return new ApiResult(true);
    }

    // 将章节从卷中移出（设为未归属状态）
    public async Task<ApiResult> RemoveChapterFromVolumeAsync(string workId, string chapterId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var chapter = await dbContext.Chapters
            .FirstOrDefaultAsync(x => x.Id == chapterId && x.WorkId == workId, cancellationToken);
        if (chapter is null)
            return new ApiResult("章节不存在。", 404);

        if (string.IsNullOrEmpty(chapter.VolumeId))
            return new ApiResult("章节未在任何卷中。", 400);

        chapter.VolumeId = string.Empty;
        chapter.UpdateBy = userId;
        chapter.UpdateAt = DateTime.Now;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 将章节 {ChapterId} 移出卷", userId, chapterId);

        return new ApiResult(true);
    }

    private static VolumeItemResponse ToResponse(VolumeEntity entity, int chapterCount)
        => new VolumeItemResponse
        {
            Id = entity.Id,
            WorkId = entity.WorkId,
            Title = entity.Title,
            Sequence = entity.Sequence,
            Summary = entity.Summary,
            ChapterCount = chapterCount
        };
}
