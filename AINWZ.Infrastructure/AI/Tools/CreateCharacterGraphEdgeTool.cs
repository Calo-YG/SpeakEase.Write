using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateCharacterGraphEdgeTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_character_graph_edge",
            Description = "在角色关系图谱中创建或更新两个节点之间的连线（关系边）。通过 id 查找已有边，存在则更新label/weight/direction，不存在则创建。支持两种节点指定策略：节点ID直接指定 / 角色名称自动查找。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["graph_id"] = new() { Type = "string", Description = "图谱ID（必填）" },
                    ["id"] = new() { Type = "string", Description = "边ID（可选），用于更新已有边" },
                    ["source_node_id"] = new() { Type = "string", Description = "源节点ID（新建时策略A，与 target_node_id 一起使用）" },
                    ["target_node_id"] = new() { Type = "string", Description = "目标节点ID（新建时策略A，与 source_node_id 一起使用）" },
                    ["source_character_name"] = new() { Type = "string", Description = "源角色名称（新建时策略B，与 target_character_name 一起使用）" },
                    ["target_character_name"] = new() { Type = "string", Description = "目标角色名称（新建时策略B，与 source_character_name 一起使用）" },
                    ["relation_type"] = new() { Type = "string", Description = "关系类型（新建必填），如: 父子/师徒/夫妻/宿敌/挚友/上下级/同门/恋人/仇人" },
                    ["label"] = new() { Type = "string", Description = "边展示标签（可选），默认为关系类型" },
                    ["weight"] = new() { Type = "integer", Description = "关系权重（可选，1-10）" },
                    ["direction"] = new() { Type = "string", Description = "方向类型（可选），directed=单向，undirected=双向" },
                    ["relationship_id"] = new() { Type = "string", Description = "关联角色关系记录ID（可选）" }
                },
                Required = ["work_id", "graph_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var graphId = args.GetString("graph_id", required: true);
        var edgeId = args.GetString("id");
        var sourceNodeId = args.GetString("source_node_id");
        var targetNodeId = args.GetString("target_node_id");
        var sourceCharName = args.GetString("source_character_name");
        var targetCharName = args.GetString("target_character_name");
        var relationType = args.GetString("relation_type");
        var label = args.GetString("label");
        var weight = args.GetInt32("weight", defaultValue: 0, min: 1, max: 10);
        var direction = args.GetString("direction");
        var relationshipId = args.GetString("relationship_id");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var graph = await db.CharacterGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == graphId && x.WorkId == workId, ct);
        if (graph == null)
            return ToolResult.Fail($"未找到图谱 {graphId}", "graph_not_found");

        if (!string.IsNullOrEmpty(edgeId))
        {
            var existing = await db.CharacterGraphEdges.FindAsync(edgeId, ct);
            if (existing == null)
                return ToolResult.Fail($"未找到边 {edgeId}", "edge_not_found");

            if (!string.IsNullOrEmpty(relationType)) existing.RelationType = relationType;
            if (args.Has("label")) existing.Label = label ?? string.Empty;
            if (weight > 0) existing.Weight = weight;
            if (args.Has("direction")) existing.Direction = direction ?? "directed";
            if (args.Has("relationship_id")) existing.RelationshipId = relationshipId ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"边已更新，ID: {existing.Id}");
        }

        if (!string.IsNullOrEmpty(sourceNodeId) && !string.IsNullOrEmpty(targetNodeId))
        {
            var srcNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == sourceNodeId && n.GraphId == graphId, ct);
            var tgtNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == targetNodeId && n.GraphId == graphId, ct);
            if (srcNode == null) return ToolResult.Fail($"未找到源节点 {sourceNodeId}", "source_node_not_found");
            if (tgtNode == null) return ToolResult.Fail($"未找到目标节点 {targetNodeId}", "target_node_not_found");
            return await CreateOrUpdateEdge(db, idGen, graphId, workId, srcNode.Id, tgtNode.Id, relationType ?? "unknown", label ?? relationType ?? "unknown", weight > 0 ? weight : 5, direction ?? "directed", relationshipId, srcNode.DisplayName, tgtNode.DisplayName, ct);
        }

        if (!string.IsNullOrEmpty(sourceCharName) && !string.IsNullOrEmpty(targetCharName))
        {
            var srcNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.GraphId == graphId && n.DisplayName == sourceCharName, ct);
            var tgtNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.GraphId == graphId && n.DisplayName == targetCharName, ct);
            if (srcNode == null) return ToolResult.Fail($"图谱中未找到角色节点「{sourceCharName}」", "source_not_in_graph");
            if (tgtNode == null) return ToolResult.Fail($"图谱中未找到角色节点「{targetCharName}」", "target_not_in_graph");
            return await CreateOrUpdateEdge(db, idGen, graphId, workId, srcNode.Id, tgtNode.Id, relationType ?? "unknown", label ?? relationType ?? "unknown", weight > 0 ? weight : 5, direction ?? "directed", relationshipId, sourceCharName, targetCharName, ct);
        }

        return ToolResult.Fail("新建边时请提供策略A（source_node_id+target_node_id）或策略B（source_character_name+target_character_name），更新时请提供 id", "missing_node_ref");
    }

    private static async Task<ToolResult> CreateOrUpdateEdge(
        SpeakEaseDbContext db, ISnowflakeIdGenerator idGen,
        string graphId, string workId,
        string sourceNodeId, string targetNodeId,
        string relationType, string label, int weight, string direction,
        string relationshipId,
        string sourceName, string targetName,
        CancellationToken ct)
    {
        var existing = await db.CharacterGraphEdges
            .FirstOrDefaultAsync(e => e.GraphId == graphId &&
                                      e.SourceNodeId == sourceNodeId &&
                                      e.TargetNodeId == targetNodeId, ct);

        if (existing != null)
        {
            existing.RelationType = relationType;
            existing.Label = label;
            existing.Weight = weight;
            if (!string.IsNullOrEmpty(direction)) existing.Direction = direction;
            if (!string.IsNullOrEmpty(relationshipId)) existing.RelationshipId = relationshipId;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"边已更新: {sourceName} →[{relationType}]→ {targetName}，权重: {weight}");
        }

        var edge = new CharacterGraphEdgeEntity
        {
            Id = idGen.NextIdString(),
            GraphId = graphId,
            WorkId = workId,
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            RelationshipId = relationshipId ?? string.Empty,
            RelationType = relationType,
            Label = label,
            Weight = weight,
            Direction = direction
        };

        await db.CharacterGraphEdges.AddAsync(edge, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"边已创建: {sourceName} →[{relationType}]→ {targetName}，边ID: {edge.Id}，权重: {weight}");
    }
}
