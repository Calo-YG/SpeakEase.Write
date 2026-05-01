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

/// <summary>
/// 大纲管理应用服务实现。
/// </summary>
public class OutlineApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<OutlineApplication> logger) : IOutlineApplication
{
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    private static OutlineNodeItemResponse ToResponse(OutlineNodeEntity x) => new()
    {
        Id = x.Id,
        WorkId = x.WorkId,
        ParentId = string.IsNullOrEmpty(x.ParentNodeId) ? null : x.ParentNodeId,
        Title = x.Title,
        Description = x.Goal,
        Sequence = x.Sequence,
        ChapterId = null
    };

    public async Task<ApiResult<List<OutlineNodeItemResponse>>> GetOutlineTreeAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<OutlineNodeItemResponse>>("作品不存在或无权访问。", 404);

        var nodes = await dbContext.OutlineNodes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(cancellationToken);

        return new ApiResult<List<OutlineNodeItemResponse>>(nodes.Select(ToResponse).ToList());
    }

    public async Task<ApiResult<OutlineNodeItemResponse>> CreateNodeAsync(string workId, SaveOutlineNodeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<OutlineNodeItemResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Title))
            return new ApiResult<OutlineNodeItemResponse>("节点标题不能为空。", 400);

        // 确保作品有主大纲，若没有则创建
        var outline = await dbContext.Outlines.FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);
        if (outline is null)
        {
            outline = new OutlineEntity
            {
                Id = idGenerator.NextIdString(),
                WorkId = workId,
                OwnerId = userId,
                Title = "主大纲",
                IsPrimary = true,
                CreateBy = userId,
                UpdateBy = userId
            };
            dbContext.Outlines.Add(outline);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var maxSeq = await dbContext.OutlineNodes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .Select(x => (int?)x.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

        var entity = new OutlineNodeEntity
        {
            Id = idGenerator.NextIdString(),
            OutlineId = outline.Id,
            WorkId = workId,
            OwnerId = userId,
            ParentNodeId = request.ParentId ?? string.Empty,
            Title = request.Title.Trim(),
            Goal = request.Description ?? string.Empty,
            Sequence = request.Sequence ?? maxSeq + 1,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.OutlineNodes.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 在作品 {WorkId} 创建大纲节点：{Title}", userId, workId, entity.Title);

        return new ApiResult<OutlineNodeItemResponse>(ToResponse(entity));
    }

    public async Task<ApiResult<OutlineNodeItemResponse>> UpdateNodeAsync(string workId, string nodeId, SaveOutlineNodeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<OutlineNodeItemResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.OutlineNodes
            .FirstOrDefaultAsync(x => x.Id == nodeId && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult<OutlineNodeItemResponse>("大纲节点不存在。", 404);

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return new ApiResult<OutlineNodeItemResponse>("节点标题不能为空。", 400);
            entity.Title = request.Title.Trim();
        }
        if (request.Description is not null) entity.Goal = request.Description;
        if (request.ParentId is not null) entity.ParentNodeId = request.ParentId;
        if (request.Sequence.HasValue) entity.Sequence = request.Sequence.Value;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<OutlineNodeItemResponse>(ToResponse(entity));
    }

    public async Task<ApiResult> DeleteNodeAsync(string workId, string nodeId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.OutlineNodes
            .FirstOrDefaultAsync(x => x.Id == nodeId && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult("大纲节点不存在。", 404);

        dbContext.OutlineNodes.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 删除大纲节点：{Title}，Id={Id}", userId, entity.Title, entity.Id);

        return new ApiResult(true);
    }
}
