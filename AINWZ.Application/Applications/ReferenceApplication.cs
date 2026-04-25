using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.References;
using SpeakEase.Write.Application.Contracts.References.Dto;
using SpeakEase.Write.Domain.Entities.Learning;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;
using System.Text.Json;

namespace SpeakEase.Write.Application.Applications;

/// <summary>
/// 参考资源应用服务实现。
/// </summary>
public class ReferenceApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<ReferenceApplication> logger) : IReferenceApplication
{
    public async Task<ApiResult<List<ReferenceWorkItemResponse>>> GetWorksAsync(ReferenceWorkQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ReferenceWorks.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(x => x.Title.Contains(request.Keyword) || x.Author.Contains(request.Keyword));

        var list = await query
            .OrderByDescending(x => x.Score)
            .Select(x => new ReferenceWorkItemResponse
            {
                Id = x.Id,
                Title = x.Title,
                Author = x.Author,
                Genre = x.Genre,
                StyleTags = x.StyleTags,
                Score = x.Score,
                Summary = x.Summary
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<ReferenceWorkItemResponse>>(list);
    }

    public async Task<ApiResult<PageResult<ReferencePassageItemResponse>>> QueryPassagesAsync(ReferencePassageQueryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = dbContext.ReferencePassages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
            query = query.Where(x => x.Content.Contains(request.Keyword) || x.TechniqueAnalysis.Contains(request.Keyword));

        if (!string.IsNullOrWhiteSpace(request.PassageType))
            query = query.Where(x => x.PassageType == request.PassageType);

        // 将标签过滤下推到 SQL 层（JSON 包含查询），避免内存过滤导致分页 total 计数不准
        if (!string.IsNullOrWhiteSpace(request.Tag))
            query = query.Where(x => x.HighlightTagsJson.Contains(request.Tag));

        var total = await query.CountAsync(cancellationToken);
        var passageIds = await query
            .OrderByDescending(x => x.FavoriteCount)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var passages = await dbContext.ReferencePassages.AsNoTracking()
            .Where(x => passageIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var workIds = passages.Select(p => p.ReferenceWorkId).Distinct().ToList();
        var works = await dbContext.ReferenceWorks.AsNoTracking()
            .Where(x => workIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var favoritedIds = await dbContext.UserPassageFavorites.AsNoTracking()
            .Where(x => x.UserId == userId && passageIds.Contains(x.PassageId))
            .Select(x => x.PassageId)
            .ToHashSetAsync(cancellationToken);

        var items = passages.Select(p =>
        {
            works.TryGetValue(p.ReferenceWorkId, out var work);
            var tags = ParseJsonList(p.HighlightTagsJson);

            return new ReferencePassageItemResponse
            {
                Id = p.Id,
                ReferenceWorkId = p.ReferenceWorkId,
                ReferenceWorkTitle = work?.Title ?? string.Empty,
                ReferenceWorkAuthor = work?.Author ?? string.Empty,
                ReferenceWorkGenre = work?.Genre ?? string.Empty,
                PassageType = p.PassageType,
                Content = p.Content,
                HighlightTags = tags,
                TechniqueAnalysis = p.TechniqueAnalysis,
                FavoriteCount = p.FavoriteCount,
                RecommendationCount = p.RecommendationCount,
                FavoritedByMe = favoritedIds.Contains(p.Id)
            };
        }).ToList();

        return new ApiResult<PageResult<ReferencePassageItemResponse>>(
            PageResult<ReferencePassageItemResponse>.Create(total, items, pageIndex, pageSize));
    }

    public async Task<ApiResult<ReferencePassageItemResponse>> GetPassageByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var passage = await dbContext.ReferencePassages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (passage is null)
            return new ApiResult<ReferencePassageItemResponse>("段落不存在。", 404);

        var work = await dbContext.ReferenceWorks.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == passage.ReferenceWorkId, cancellationToken);

        var favoritedByMe = await dbContext.UserPassageFavorites.AnyAsync(
            x => x.UserId == userId && x.PassageId == id, cancellationToken);

        return new ApiResult<ReferencePassageItemResponse>(new ReferencePassageItemResponse
        {
            Id = passage.Id,
            ReferenceWorkId = passage.ReferenceWorkId,
            ReferenceWorkTitle = work?.Title ?? string.Empty,
            ReferenceWorkAuthor = work?.Author ?? string.Empty,
            ReferenceWorkGenre = work?.Genre ?? string.Empty,
            PassageType = passage.PassageType,
            Content = passage.Content,
            HighlightTags = ParseJsonList(passage.HighlightTagsJson),
            TechniqueAnalysis = passage.TechniqueAnalysis,
            FavoriteCount = passage.FavoriteCount,
            RecommendationCount = passage.RecommendationCount,
            FavoritedByMe = favoritedByMe
        });
    }

    public async Task<ApiResult<ReferencePassageItemResponse>> AddPassageAsync(SaveReferencePassageRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return new ApiResult<ReferencePassageItemResponse>("段落内容不能为空。", 400);

        string referenceWorkId = request.ReferenceWorkId ?? string.Empty;
        ReferencePassageItemResponse response;

        // 开启事务：查找/创建书籍 + 保存段落必须原子完成，避免孤儿 ReferenceWork
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 如果没有提供 referenceWorkId，则使用书名+作者查找或创建 ReferenceWork
            if (string.IsNullOrWhiteSpace(referenceWorkId) && !string.IsNullOrWhiteSpace(request.BookTitle))
            {
                var work = await dbContext.ReferenceWorks
                    .FirstOrDefaultAsync(x => x.Title == request.BookTitle, cancellationToken);

                if (work is null)
                {
                    work = new ReferenceWorkEntity
                    {
                        Id = idGenerator.NextIdString(),
                        Title = request.BookTitle,
                        Author = request.Author ?? string.Empty,
                        Genre = request.Genre ?? string.Empty,
                        CreateBy = userContext.UserId,
                        UpdateBy = userContext.UserId
                    };
                    dbContext.ReferenceWorks.Add(work);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                referenceWorkId = work.Id;
            }

            var entity = new ReferencePassageEntity
            {
                Id = idGenerator.NextIdString(),
                ReferenceWorkId = referenceWorkId,
                PassageType = request.PassageType,
                Content = request.Content,
                HighlightTagsJson = SerializeJsonList(request.HighlightTags ?? new()),
                TechniqueAnalysis = request.TechniqueAnalysis ?? string.Empty,
                CreateBy = userContext.UserId,
                UpdateBy = userContext.UserId
            };

            dbContext.ReferencePassages.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var refWork = string.IsNullOrEmpty(referenceWorkId) ? null :
                await dbContext.ReferenceWorks.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == referenceWorkId, cancellationToken);

            logger.LogInformation("用户 {UserId} 添加参考段落，Id={Id}", userContext.UserId, entity.Id);

            response = new ReferencePassageItemResponse
            {
                Id = entity.Id,
                ReferenceWorkId = entity.ReferenceWorkId,
                ReferenceWorkTitle = refWork?.Title ?? string.Empty,
                ReferenceWorkAuthor = refWork?.Author ?? string.Empty,
                ReferenceWorkGenre = refWork?.Genre ?? string.Empty,
                PassageType = entity.PassageType,
                Content = entity.Content,
                HighlightTags = request.HighlightTags ?? new(),
                TechniqueAnalysis = entity.TechniqueAnalysis,
                FavoriteCount = 0,
                RecommendationCount = 0,
                FavoritedByMe = false
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "添加参考段落事务失败，已回滚");
            return new ApiResult<ReferencePassageItemResponse>("添加段落失败，请稍后重试。", 500);
        }

        return new ApiResult<ReferencePassageItemResponse>(response);
    }

    public async Task<ApiResult> DeletePassageAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferencePassages
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return new ApiResult("段落不存在。", 404);

        dbContext.ReferencePassages.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("删除参考段落，Id={Id}", id);
        return new ApiResult(true);
    }

    public async Task<ApiResult<bool>> ToggleFavoriteAsync(string passageId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        var passage = await dbContext.ReferencePassages
            .FirstOrDefaultAsync(x => x.Id == passageId, cancellationToken);

        if (passage is null)
            return new ApiResult<bool>("段落不存在。", 404);

        var existing = await dbContext.UserPassageFavorites
            .FirstOrDefaultAsync(x => x.UserId == userId && x.PassageId == passageId, cancellationToken);

        bool isFavorited;
        if (existing is not null)
        {
            dbContext.UserPassageFavorites.Remove(existing);
            passage.FavoriteCount = Math.Max(0, passage.FavoriteCount - 1);
            isFavorited = false;
        }
        else
        {
            dbContext.UserPassageFavorites.Add(new UserPassageFavoriteEntity
            {
                Id = idGenerator.NextIdString(),
                UserId = userId,
                PassageId = passageId,
                CreateBy = userId,
                UpdateBy = userId
            });
            passage.FavoriteCount++;
            isFavorited = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult<bool>(isFavorited);
    }

    private static List<string> ParseJsonList(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
    }

    private static string SerializeJsonList(List<string> list)
        => JsonSerializer.Serialize(list);
}
