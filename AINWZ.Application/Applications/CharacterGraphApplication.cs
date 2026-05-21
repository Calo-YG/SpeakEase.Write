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

public class CharacterGraphApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<CharacterGraphApplication> logger) : ICharacterGraphApplication
{
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    public async Task<ApiResult<List<CharacterGraphResponse>>> ListGraphsAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<CharacterGraphResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.CharacterGraphs.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderByDescending(x => x.CreateAt)
            .Select(x => new CharacterGraphResponse
            {
                Id = x.Id, WorkId = x.WorkId, Name = x.Name,
                Description = x.Description, Version = x.Version,
                Status = x.Status, LayoutJson = x.LayoutJson,
                CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<CharacterGraphResponse>>(list);
    }

    public async Task<ApiResult<CharacterGraphResponse>> GetGraphDetailAsync(string workId, string graphId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterGraphResponse>("作品不存在或无权访问。", 404);

        var graph = await dbContext.CharacterGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == graphId && x.WorkId == workId, cancellationToken);

        if (graph == null)
            return new ApiResult<CharacterGraphResponse>("图谱不存在。", 404);

        var nodes = await dbContext.CharacterGraphNodes.AsNoTracking()
            .Where(n => n.GraphId == graphId)
            .OrderByDescending(n => n.Importance)
            .Select(n => new CharacterGraphNodeResponse
            {
                Id = n.Id, CharacterId = n.CharacterId,
                DisplayName = n.DisplayName, NodeType = n.NodeType,
                Importance = n.Importance, X = n.X, Y = n.Y,
                StyleJson = n.StyleJson
            })
            .ToListAsync(cancellationToken);

        var edges = await dbContext.CharacterGraphEdges.AsNoTracking()
            .Where(e => e.GraphId == graphId)
            .Select(e => new CharacterGraphEdgeResponse
            {
                Id = e.Id, SourceNodeId = e.SourceNodeId,
                TargetNodeId = e.TargetNodeId, RelationType = e.RelationType,
                Label = e.Label, Weight = e.Weight, Direction = e.Direction
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<CharacterGraphResponse>(new CharacterGraphResponse
        {
            Id = graph.Id, WorkId = graph.WorkId, Name = graph.Name,
            Description = graph.Description, Version = graph.Version,
            Status = graph.Status, LayoutJson = graph.LayoutJson,
            Nodes = nodes, Edges = edges, CreatedAt = graph.CreateAt
        });
    }

    public async Task<ApiResult<CharacterGraphResponse>> CreateGraphAsync(string workId, SaveCharacterGraphRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterGraphResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Name))
            return new ApiResult<CharacterGraphResponse>("图谱名称不能为空。", 400);

        var existing = await dbContext.CharacterGraphs.AsNoTracking()
            .AnyAsync(x => x.WorkId == workId && x.Name == request.Name, cancellationToken);

        if (existing)
            return new ApiResult<CharacterGraphResponse>("同名图谱已存在。", 400);

        var entity = new CharacterGraphEntity
        {
            Id = idGenerator.NextIdString(),
            WorkId = workId,
            OwnerId = userId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            LayoutJson = request.LayoutJson ?? string.Empty,
            Version = 1,
            Status = "draft",
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.CharacterGraphs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<CharacterGraphResponse>(new CharacterGraphResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Name = entity.Name,
            Description = entity.Description, Version = entity.Version,
            Status = entity.Status, LayoutJson = entity.LayoutJson,
            CreatedAt = entity.CreateAt
        });
    }

    public async Task<ApiResult> DeleteGraphAsync(string workId, string graphId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.CharacterGraphs
            .FirstOrDefaultAsync(x => x.Id == graphId && x.WorkId == workId, cancellationToken);

        if (entity == null)
            return new ApiResult("图谱不存在。", 404);

        var nodes = await dbContext.CharacterGraphNodes.Where(n => n.GraphId == graphId).ToListAsync(cancellationToken);
        var edges = await dbContext.CharacterGraphEdges.Where(e => e.GraphId == graphId).ToListAsync(cancellationToken);

        dbContext.CharacterGraphEdges.RemoveRange(edges);
        dbContext.CharacterGraphNodes.RemoveRange(nodes);
        dbContext.CharacterGraphs.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult(true);
    }

    public async Task<ApiResult<CharacterGraphResponse>> UpdateLayoutAsync(string workId, string graphId, UpdateGraphLayoutRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterGraphResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.CharacterGraphs
            .FirstOrDefaultAsync(x => x.Id == graphId && x.WorkId == workId, cancellationToken);

        if (entity == null)
            return new ApiResult<CharacterGraphResponse>("图谱不存在。", 404);

        entity.LayoutJson = request.LayoutJson;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<CharacterGraphResponse>(new CharacterGraphResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Name = entity.Name,
            LayoutJson = entity.LayoutJson, CreatedAt = entity.CreateAt
        });
    }
}
