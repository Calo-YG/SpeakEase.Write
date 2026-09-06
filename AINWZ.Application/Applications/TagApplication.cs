using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Application.Contracts.Tags;
using SpeakEase.Write.Application.Contracts.Tags.Dto;
using SpeakEase.Write.Domain.Entities.Tags;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEaseDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Applications;

// 标签管理应用服务：提供标签的CRUD和热门标签查询，标签用于内容分类
public class TagApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    ILogger<TagApplication> logger) : ITagApplication
{
    // 实体→响应DTO映射
    private static TagItemResponse ToResponse(TagEntity x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Category = x.Category,
        Color = x.Color,
        UsageCount = x.UsageCount
    };

    // 按分类查询标签列表，不传分类则返回全部标签；按使用次数降序、名称升序排列
    public async Task<ApiResult<List<TagItemResponse>>> ListTagsAsync(string category, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Tags.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);

        var list = await query
            .OrderByDescending(x => x.UsageCount)
            .ThenBy(x => x.Name)
            .Select(x => new TagItemResponse
            {
                Id = x.Id,
                Name = x.Name,
                Category = x.Category,
                Color = x.Color,
                UsageCount = x.UsageCount
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<TagItemResponse>>(list);
    }

    // 创建新标签：默认分类为"content"，默认颜色为灰色
    public async Task<ApiResult<TagItemResponse>> CreateTagAsync(SaveTagRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return new ApiResult<TagItemResponse>("标签名称不能为空。", 400);

        var entity = new TagEntity
        {
            Id = idGenerator.NextIdString(),
            Name = request.Name.Trim(),
            Category = request.Category ?? "content",
            Color = request.Color ?? "#6b7280",
            UsageCount = 0,
            CreateBy = "system",
            UpdateBy = "system"
        };

        dbContext.Tags.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("创建标签：{Name}", entity.Name);

        return new ApiResult<TagItemResponse>(ToResponse(entity));
    }

    // 更新标签：支持部分更新（Name、Category、Color可单独修改）
    public async Task<ApiResult<TagItemResponse>> UpdateTagAsync(string id, SaveTagRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Tags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return new ApiResult<TagItemResponse>("标签不存在。", 404);

        if (request.Name is not null) entity.Name = request.Name.Trim();
        if (request.Category is not null) entity.Category = request.Category;
        if (request.Color is not null) entity.Color = request.Color;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<TagItemResponse>(ToResponse(entity));
    }

    // 删除标签
    public async Task<ApiResult> DeleteTagAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Tags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return new ApiResult("标签不存在。", 404);

        dbContext.Tags.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("删除标签：{Name}，Id={Id}", entity.Name, entity.Id);
        return new ApiResult(true);
    }

    // 获取热门标签：按使用次数降序取前N条（默认20条）
    public async Task<ApiResult<List<TagItemResponse>>> GetHotTagsAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 20;
        var list = await dbContext.Tags.AsNoTracking()
            .OrderByDescending(x => x.UsageCount)
            .Take(limit)
            .Select(x => new TagItemResponse
            {
                Id = x.Id,
                Name = x.Name,
                Category = x.Category,
                Color = x.Color,
                UsageCount = x.UsageCount
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<TagItemResponse>>(list);
    }
}
