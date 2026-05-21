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

public class TimelineApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<TimelineApplication> logger) : ITimelineApplication
{
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    private async Task<bool> ChapterExistsAsync(string chapterId, string workId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(chapterId)) return true;
        return await dbContext.Chapters.AnyAsync(c => c.Id == chapterId && c.WorkId == workId, ct);
    }

    private async Task<bool> CharactersExistAsync(List<string> characterIds, string workId, CancellationToken ct)
    {
        if (characterIds is null || characterIds.Count == 0) return true;
        var existingCount = await dbContext.Characters
            .CountAsync(c => characterIds.Contains(c.Id) && c.WorkId == workId, ct);
        return existingCount == characterIds.Select(x => x).Distinct().Count();
    }

    public async Task<ApiResult<List<TimelineEventItemResponse>>> ListTimelineEventsAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<TimelineEventItemResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.TimelineEvents.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.EventTime)
            .Select(x => new TimelineEventItemResponse
            {
                Id = x.Id, WorkId = x.WorkId, ChapterId = x.ChapterId,
                Title = x.Title, Description = x.Description,
                EventTime = x.EventTime, EventType = x.EventType,
                RelatedCharacterIds = x.RelatedCharacterIds, CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<TimelineEventItemResponse>>(list);
    }

    public async Task<ApiResult<TimelineEventItemResponse>> GetTimelineEventByIdAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<TimelineEventItemResponse>("作品不存在或无权访问。", 404);

        var result = await dbContext.TimelineEvents.AsNoTracking()
            .Where(x => x.Id == id && x.WorkId == workId)
            .Select(x => new TimelineEventItemResponse
            {
                Id = x.Id, WorkId = x.WorkId, ChapterId = x.ChapterId,
                Title = x.Title, Description = x.Description,
                EventTime = x.EventTime, EventType = x.EventType,
                RelatedCharacterIds = x.RelatedCharacterIds, CreatedAt = x.CreateAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
            return new ApiResult<TimelineEventItemResponse>("时间线事件不存在。", 404);

        return new ApiResult<TimelineEventItemResponse>(result);
    }

    public async Task<ApiResult<TimelineEventItemResponse>> CreateTimelineEventAsync(string workId, SaveTimelineEventRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<TimelineEventItemResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Title))
            return new ApiResult<TimelineEventItemResponse>("事件标题不能为空。", 400);

        if (!string.IsNullOrEmpty(request.ChapterId) && !await ChapterExistsAsync(request.ChapterId, workId, cancellationToken))
            return new ApiResult<TimelineEventItemResponse>("关联章节不存在。", 400);

        if (request.RelatedCharacterIds is not null && request.RelatedCharacterIds.Count > 0
            && !await CharactersExistAsync(request.RelatedCharacterIds, workId, cancellationToken))
            return new ApiResult<TimelineEventItemResponse>("关联角色不存在或属于其他作品。", 400);

        var entity = new TimelineEventEntity
        {
            Id = idGenerator.NextIdString(),
            WorkId = workId,
            OwnerId = userId,
            Title = request.Title.Trim(),
            Description = request.Description ?? string.Empty,
            ChapterId = request.ChapterId ?? string.Empty,
            EventTime = request.EventTime ?? DateTime.Now,
            EventType = request.EventType ?? string.Empty,
            RelatedCharacterIds = request.RelatedCharacterIds ?? new List<string>(),
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.TimelineEvents.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 在作品 {WorkId} 创建时间线事件：{Title}，Type={EventType}",
            userId, workId, entity.Title, entity.EventType);

        return new ApiResult<TimelineEventItemResponse>(new TimelineEventItemResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, ChapterId = entity.ChapterId,
            Title = entity.Title, Description = entity.Description,
            EventTime = entity.EventTime, EventType = entity.EventType,
            RelatedCharacterIds = entity.RelatedCharacterIds, CreatedAt = entity.CreateAt
        });
    }

    public async Task<ApiResult<TimelineEventItemResponse>> UpdateTimelineEventAsync(string workId, string id, SaveTimelineEventRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<TimelineEventItemResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.TimelineEvents
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult<TimelineEventItemResponse>("时间线事件不存在。", 404);

        if (!string.IsNullOrEmpty(request.ChapterId) && !await ChapterExistsAsync(request.ChapterId, workId, cancellationToken))
            return new ApiResult<TimelineEventItemResponse>("关联章节不存在。", 400);

        if (request.RelatedCharacterIds is not null && request.RelatedCharacterIds.Count > 0
            && !await CharactersExistAsync(request.RelatedCharacterIds, workId, cancellationToken))
            return new ApiResult<TimelineEventItemResponse>("关联角色不存在或属于其他作品。", 400);

        if (request.Title is not null) entity.Title = request.Title.Trim();
        if (request.Description is not null) entity.Description = request.Description;
        if (request.ChapterId is not null) entity.ChapterId = request.ChapterId;
        if (request.EventTime.HasValue) entity.EventTime = request.EventTime.Value;
        if (request.EventType is not null) entity.EventType = request.EventType;
        if (request.RelatedCharacterIds is not null) entity.RelatedCharacterIds = request.RelatedCharacterIds;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<TimelineEventItemResponse>(new TimelineEventItemResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, ChapterId = entity.ChapterId,
            Title = entity.Title, Description = entity.Description,
            EventTime = entity.EventTime, EventType = entity.EventType,
            RelatedCharacterIds = entity.RelatedCharacterIds, CreatedAt = entity.CreateAt
        });
    }

    public async Task<ApiResult> DeleteTimelineEventAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.TimelineEvents
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult("时间线事件不存在。", 404);

        dbContext.TimelineEvents.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 删除时间线事件：{Title}，Id={Id}", userId, entity.Title, entity.Id);

        return new ApiResult(true);
    }

    public async Task<ApiResult<List<TimelineEventItemResponse>>> ListEventsBeforeDeleteAsync(string workId, string eventId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<TimelineEventItemResponse>>("作品不存在或无权访问。", 404);

        var target = await dbContext.TimelineEvents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == eventId && x.WorkId == workId, cancellationToken);

        if (target is null)
            return new ApiResult<List<TimelineEventItemResponse>>("事件不存在。", 404);

        var dependentEvents = await dbContext.TimelineEvents.AsNoTracking()
            .Where(x => x.WorkId == workId && x.EventTime > target.EventTime
                && x.RelatedCharacterIds.Any(id => target.RelatedCharacterIds.Contains(id)))
            .OrderBy(x => x.EventTime)
            .Take(20)
            .Select(x => new TimelineEventItemResponse
            {
                Id = x.Id, WorkId = x.WorkId, ChapterId = x.ChapterId,
                Title = x.Title, Description = x.Description,
                EventTime = x.EventTime, EventType = x.EventType,
                RelatedCharacterIds = x.RelatedCharacterIds, CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<TimelineEventItemResponse>>(dependentEvents);
    }
}
