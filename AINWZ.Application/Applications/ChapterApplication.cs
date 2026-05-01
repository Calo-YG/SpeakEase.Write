using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Snapshot;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

/// <summary>
/// 章节管理应用服务实现。
/// </summary>
public class ChapterApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<ChapterApplication> logger,
    IBlackboardUpdater blackboardUpdater) : IChapterApplication
{
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    public async Task<ApiResult<List<ChapterItemResponse>>> ListChaptersAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<ChapterItemResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.Chapters.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Sequence)
            .Select(x => new ChapterItemResponse
            {
                Id = x.Id, WorkId = x.WorkId, VolumeId = x.VolumeId, Title = x.Title, Sequence = x.Sequence,
                WordCount = x.WordCount, Status = x.Status, Summary = x.Summary,
                AuthorNotes = x.AuthorNotes, LastContentSavedAt = x.LastContentSavedAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<ChapterItemResponse>>(list);
    }

    public async Task<ApiResult<ChapterDetailResponse>> GetChapterDetailAsync(string workId, string chapterId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<ChapterDetailResponse>("作品不存在或无权访问。", 404);

        var chapter = await dbContext.Chapters.AsNoTracking()
            .Where(x => x.Id == chapterId && x.WorkId == workId)
            .Select(x => new ChapterDetailResponse
            {
                Id = x.Id, WorkId = x.WorkId, VolumeId = x.VolumeId, Title = x.Title, Sequence = x.Sequence,
                WordCount = x.WordCount, Status = x.Status, Summary = x.Summary,
                AuthorNotes = x.AuthorNotes, LastContentSavedAt = x.LastContentSavedAt,
                Content = x.Content
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (chapter is null)
            return new ApiResult<ChapterDetailResponse>("章节不存在。", 404);

        return new ApiResult<ChapterDetailResponse>(chapter);
    }

    public async Task<ApiResult<ChapterDetailResponse>> CreateChapterAsync(string workId, CreateChapterRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<ChapterDetailResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Title))
            return new ApiResult<ChapterDetailResponse>("章节标题不能为空。", 400);

        var maxSeq = await dbContext.Chapters.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .Select(x => (int?)x.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

        var entity = new ChapterEntity
        {
            Id = idGenerator.NextIdString(),
            WorkId = workId,
            OwnerId = userId,
            Title = request.Title.Trim(),
            Sequence = request.Sequence ?? maxSeq + 1,
            Status = "draft",
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.Chapters.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 在作品 {WorkId} 创建章节：{Title}", userId, workId, entity.Title);

        return new ApiResult<ChapterDetailResponse>(new ChapterDetailResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, VolumeId = entity.VolumeId, Title = entity.Title,
            Sequence = entity.Sequence, WordCount = 0, Status = entity.Status,
            Summary = entity.Summary, AuthorNotes = entity.AuthorNotes,
            LastContentSavedAt = null, Content = string.Empty
        });
    }

    public async Task<ApiResult<ChapterDetailResponse>> UpdateChapterAsync(string workId, string chapterId, UpdateChapterRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<ChapterDetailResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.Chapters
            .FirstOrDefaultAsync(x => x.Id == chapterId && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult<ChapterDetailResponse>("章节不存在。", 404);

        if (request.Title is not null) entity.Title = request.Title.Trim();
        if (request.Status is not null) entity.Status = request.Status;
        if (request.AuthorNotes is not null) entity.AuthorNotes = request.AuthorNotes;

        if (request.Content is not null)
        {
            entity.Content = request.Content;
            entity.WordCount = CountWords(request.Content);
            entity.LastContentSavedAt = DateTime.UtcNow;
        }

        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.UtcNow;

        // 开启事务：章节保存 + 作品总字数更新必须原子完成
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            // 更新作品总字数
            var totalWords = await dbContext.Chapters.AsNoTracking()
                .Where(x => x.WorkId == workId)
                .SumAsync(x => x.WordCount, cancellationToken);

            await dbContext.Works
                .Where(x => x.Id == workId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalWordCount, totalWords)
                                          .SetProperty(x => x.UpdateAt, DateTime.UtcNow), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "更新章节 {ChapterId} 事务失败，已回滚", chapterId);
            return new ApiResult<ChapterDetailResponse>("章节保存失败，请稍后重试。", 500);
        }

        if (request.Content is not null)
            blackboardUpdater.UpdateChapterContent(chapterId, entity.Content, entity.Summary);

        return new ApiResult<ChapterDetailResponse>(new ChapterDetailResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, VolumeId = entity.VolumeId, Title = entity.Title,
            Sequence = entity.Sequence, WordCount = entity.WordCount, Status = entity.Status,
            Summary = entity.Summary, AuthorNotes = entity.AuthorNotes,
            LastContentSavedAt = entity.LastContentSavedAt, Content = entity.Content
        });
    }

    public async Task<ApiResult> DeleteChapterAsync(string workId, string chapterId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.Chapters
            .FirstOrDefaultAsync(x => x.Id == chapterId && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult("章节不存在。", 404);

        dbContext.Chapters.Remove(entity);

        // 开启事务：章节删除 + 作品总字数回算必须原子完成
        int newTotalWords = 0;
        await using var delTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            // 级联更新作品总字数（删除章节后重新计算）
            newTotalWords = await dbContext.Chapters.AsNoTracking()
                .Where(x => x.WorkId == workId)
                .SumAsync(x => x.WordCount, cancellationToken);

            await dbContext.Works
                .Where(x => x.Id == workId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalWordCount, newTotalWords)
                                          .SetProperty(x => x.UpdateAt, DateTime.UtcNow), cancellationToken);

            await delTransaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await delTransaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "删除章节 {ChapterId} 事务失败，已回滚", chapterId);
            return new ApiResult("章节删除失败，请稍后重试。", 500);
        }

        logger.LogInformation("用户 {UserId} 删除章节：{Title}，Id={Id}，作品 {WorkId} 总字数更新为 {TotalWords}",
            userId, entity.Title, entity.Id, workId, newTotalWords);

        blackboardUpdater.RemoveChapter(chapterId);

        return new ApiResult(true);
    }

    private static int CountWords(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return 0;
        return content.Count(c => !char.IsWhiteSpace(c));
    }
}
