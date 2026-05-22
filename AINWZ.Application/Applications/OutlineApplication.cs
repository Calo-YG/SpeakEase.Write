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

// 大纲管理应用服务：管理作品的树形大纲结构，支持节点的增删改查，自动创建主大纲
public class OutlineApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<OutlineApplication> logger) : IOutlineApplication
{
    // 验证当前用户是否拥有该作品的操作权限
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    // 实体→响应DTO映射（ParentId为string.Empty时转为null）
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

    // 获取作品的大纲树：查询所有节点并按序号排序
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

    // 创建大纲节点：如果作品没有主大纲则自动创建，节点序号自动递增
    public async Task<ApiResult<OutlineNodeItemResponse>> CreateNodeAsync(string workId, SaveOutlineNodeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<OutlineNodeItemResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Title))
            return new ApiResult<OutlineNodeItemResponse>("节点标题不能为空。", 400);

        // 确保作品有主大纲，若没有则创建（每个作品至少有一个"主大纲"）
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

        // 获取当前最大序号，新节点序号+1（若请求未指定序号）
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

    // 更新大纲节点：支持部分更新（Title、Description、ParentId、Sequence可单独修改）
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
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<OutlineNodeItemResponse>(ToResponse(entity));
    }

    // 删除大纲节点
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
