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

// 角色关系应用服务：管理作品中角色之间的关联关系，支持增删改查和环形检测
public class CharacterRelationshipApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<CharacterRelationshipApplication> logger) : ICharacterRelationshipApplication
{
    // 校验用户是否为作品的拥有者
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    // 校验角色是否存在于指定作品中
    private async Task<bool> CharacterExistsAsync(string characterId, string workId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(characterId)) return false;
        return await dbContext.Characters.AnyAsync(c => c.Id == characterId && c.WorkId == workId, ct);
    }

    // 列出作品下所有角色关系，按关系类型排序
    public async Task<ApiResult<List<CharacterRelationshipResponse>>> ListRelationshipsAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<CharacterRelationshipResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.CharacterRelationships.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.RelationshipType)
            .Select(x => new CharacterRelationshipResponse
            {
                Id = x.Id, WorkId = x.WorkId,
                SourceCharacterId = x.SourceCharacterId, TargetCharacterId = x.TargetCharacterId,
                RelationshipType = x.RelationshipType, Description = x.Description,
                Intensity = x.Intensity, CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<CharacterRelationshipResponse>>(list);
    }

    // 创建角色关系：校验源/目标角色存在性、防重复、防自引用
    public async Task<ApiResult<CharacterRelationshipResponse>> CreateRelationshipAsync(string workId, SaveCharacterRelationshipRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterRelationshipResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.SourceCharacterId))
            return new ApiResult<CharacterRelationshipResponse>("源角色不能为空。", 400);
        if (string.IsNullOrWhiteSpace(request.TargetCharacterId))
            return new ApiResult<CharacterRelationshipResponse>("目标角色不能为空。", 400);
        if (request.SourceCharacterId == request.TargetCharacterId)
            return new ApiResult<CharacterRelationshipResponse>("角色不能与自身建立关系。", 400);

        if (!await CharacterExistsAsync(request.SourceCharacterId, workId, cancellationToken))
            return new ApiResult<CharacterRelationshipResponse>("源角色不存在或不属于该作品。", 400);
        if (!await CharacterExistsAsync(request.TargetCharacterId, workId, cancellationToken))
            return new ApiResult<CharacterRelationshipResponse>("目标角色不存在或不属于该作品。", 400);

        var existing = await dbContext.CharacterRelationships.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId
                && x.SourceCharacterId == request.SourceCharacterId
                && x.TargetCharacterId == request.TargetCharacterId
                && x.RelationshipType == request.RelationshipType, cancellationToken);
        if (existing is not null)
            return new ApiResult<CharacterRelationshipResponse>("相同角色之间已存在同类型关系。", 400);

        var entity = new CharacterRelationshipEntity
        {
            Id = idGenerator.NextIdString(),
            WorkId = workId,
            OwnerId = userId,
            SourceCharacterId = request.SourceCharacterId,
            TargetCharacterId = request.TargetCharacterId,
            RelationshipType = request.RelationshipType,
            Description = request.Description ?? string.Empty,
            Intensity = request.Intensity ?? 5,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.CharacterRelationships.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 创建角色关系：{Source}->{Target}({Type})",
            userId, request.SourceCharacterId, request.TargetCharacterId, request.RelationshipType);

        return new ApiResult<CharacterRelationshipResponse>(new CharacterRelationshipResponse
        {
            Id = entity.Id, WorkId = entity.WorkId,
            SourceCharacterId = entity.SourceCharacterId, TargetCharacterId = entity.TargetCharacterId,
            RelationshipType = entity.RelationshipType, Description = entity.Description,
            Intensity = entity.Intensity, CreatedAt = entity.CreateAt
        });
    }

    // 更新角色关系：部分字段更新，允许修改源/目标角色和关系属性
    public async Task<ApiResult<CharacterRelationshipResponse>> UpdateRelationshipAsync(string workId, string id, SaveCharacterRelationshipRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterRelationshipResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.CharacterRelationships
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null)
            return new ApiResult<CharacterRelationshipResponse>("角色关系不存在。", 404);

        var newSource = request.SourceCharacterId ?? entity.SourceCharacterId;
        var newTarget = request.TargetCharacterId ?? entity.TargetCharacterId;

        if (newSource == newTarget)
            return new ApiResult<CharacterRelationshipResponse>("角色不能与自身建立关系。", 400);

        if (!await CharacterExistsAsync(newSource, workId, cancellationToken))
            return new ApiResult<CharacterRelationshipResponse>("源角色不存在或不属于该作品。", 400);
        if (!await CharacterExistsAsync(newTarget, workId, cancellationToken))
            return new ApiResult<CharacterRelationshipResponse>("目标角色不存在或不属于该作品。", 400);

        if (request.SourceCharacterId is not null) entity.SourceCharacterId = request.SourceCharacterId;
        if (request.TargetCharacterId is not null) entity.TargetCharacterId = request.TargetCharacterId;
        if (request.RelationshipType is not null) entity.RelationshipType = request.RelationshipType;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.Intensity.HasValue) entity.Intensity = request.Intensity.Value;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<CharacterRelationshipResponse>(new CharacterRelationshipResponse
        {
            Id = entity.Id, WorkId = entity.WorkId,
            SourceCharacterId = entity.SourceCharacterId, TargetCharacterId = entity.TargetCharacterId,
            RelationshipType = entity.RelationshipType, Description = entity.Description,
            Intensity = entity.Intensity, CreatedAt = entity.CreateAt
        });
    }

    // 删除角色关系
    public async Task<ApiResult> DeleteRelationshipAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.CharacterRelationships
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null)
            return new ApiResult("角色关系不存在。", 404);

        dbContext.CharacterRelationships.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult(true);
    }

    // 环形检测：基于DFS遍历关系图，检测角色间是否存在循环引用路径
    public async Task<ApiResult<Dictionary<string, List<string>>>> DetectCirclesAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<Dictionary<string, List<string>>>("作品不存在或无权访问。", 404);

        // 加载作品下所有角色关系，仅取源/目标ID
        var allRelations = await dbContext.CharacterRelationships.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .Select(x => new { x.SourceCharacterId, x.TargetCharacterId, x.RelationshipType })
            .ToListAsync(cancellationToken);

        // 构建有向图邻接表：源角色 → 目标角色列表
        var graph = new Dictionary<string, List<string>>();
        foreach (var rel in allRelations)
        {
            if (!graph.ContainsKey(rel.SourceCharacterId))
                graph[rel.SourceCharacterId] = new List<string>();
            graph[rel.SourceCharacterId].Add(rel.TargetCharacterId);
        }

        var circles = new Dictionary<string, List<string>>();

        // 深度优先搜索检测环路：从某个节点出发，能否回到自身（且路径长度>1）
        bool Dfs(string current, string target, List<string> path, HashSet<string> visited)
        {
            if (current == target && path.Count > 1)
            {
                // 找到环路，记录完整路径
                var circlePath = new List<string>(path) { target };
                circles[target] = circlePath;
                return true;
            }
            if (visited.Contains(current)) return false;
            visited.Add(current);

            if (!graph.ContainsKey(current)) return false;
            foreach (var next in graph[current])
            {
                if (Dfs(next, target, new List<string>(path) { current }, new HashSet<string>(visited)))
                    return true;
            }
            return false;
        }

        // 对图中每个节点尝试检测其是否为环的起点
        foreach (var node in graph.Keys)
        {
            if (!circles.ContainsKey(node))
                Dfs(node, node, new List<string>(), new HashSet<string>());
        }

        logger.LogInformation("用户 {UserId} 对作品 {WorkId} 执行关系图谱环形检测，发现 {Count} 个环",
            userId, workId, circles.Count);

        return new ApiResult<Dictionary<string, List<string>>>(circles);
    }
}
