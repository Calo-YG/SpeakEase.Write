using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

public class VolumeApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<VolumeApplication> logger) : IVolumeApplication
{
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

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

        var maxSeqInTarget = await dbContext.Chapters.AsNoTracking()
            .Where(x => x.VolumeId == request.TargetVolumeId)
            .Select(x => (int?)x.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

        var sourceChapters = await dbContext.Chapters
            .Where(x => x.VolumeId == volumeId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(cancellationToken);

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
