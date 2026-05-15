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
            Description = "在角色关系图谱中创建两个节点之间的连线（关系边）。可通过节点ID或角色名称指定两端，支持关联已有的角色关系记录",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["graph_id"] = new() { Type = "string", Description = "图谱ID（必填），从 create_character_graph 获取" },
                    ["source_node_id"] = new() { Type = "string", Description = "源节点ID（二选一策略A：与 target_node_id 一起使用）" },
                    ["target_node_id"] = new() { Type = "string", Description = "目标节点ID（二选一策略A：与 source_node_id 一起使用）" },
                    ["source_character_name"] = new() { Type = "string", Description = "源角色名称（二选一策略B：与 target_character_name 一起使用，自动查找节点）" },
                    ["target_character_name"] = new() { Type = "string", Description = "目标角色名称（二选一策略B：与 source_character_name 一起使用）" },
                    ["relation_type"] = new() { Type = "string", Description = "关系类型（必填），如: 父子/师徒/夫妻/宿敌/挚友/上下级/同门/恋人/仇人" },
                    ["label"] = new() { Type = "string", Description = "边展示标签（可选），默认为关系类型" },
                    ["weight"] = new() { Type = "integer", Description = "关系权重（可选，1-10，默认5）" },
                    ["direction"] = new() { Type = "string", Description = "方向类型（可选），directed=单向，undirected=双向，默认 directed" },
                    ["relationship_id"] = new() { Type = "string", Description = "关联角色关系记录ID（可选），关联已有的 CharacterRelationship 记录" }
                },
                Required = ["work_id", "graph_id", "relation_type"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var graphId = args.GetString("graph_id", required: true);
        var sourceNodeId = args.GetString("source_node_id");
        var targetNodeId = args.GetString("target_node_id");
        var sourceCharName = args.GetString("source_character_name");
        var targetCharName = args.GetString("target_character_name");
        var relationType = args.GetString("relation_type", required: true);
        var label = args.GetString("label");
        var weight = args.GetInt32("weight", defaultValue: 5, min: 1, max: 10);
        var direction = args.GetString("direction") ?? "directed";
        var relationshipId = args.GetString("relationship_id");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var graph = await db.CharacterGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == graphId && x.WorkId == workId, ct);

        if (graph == null)
            return ToolResult.Fail($"未找到图谱 {graphId}", "graph_not_found");

        if (!string.IsNullOrEmpty(sourceNodeId) && !string.IsNullOrEmpty(targetNodeId))
        {
            var srcNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == sourceNodeId && n.GraphId == graphId, ct);
            var tgtNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == targetNodeId && n.GraphId == graphId, ct);

            if (srcNode == null) return ToolResult.Fail($"未找到源节点 {sourceNodeId}", "source_node_not_found");
            if (tgtNode == null) return ToolResult.Fail($"未找到目标节点 {targetNodeId}", "target_node_not_found");

            return await CreateEdge(db, idGen, graphId, workId, srcNode.Id, tgtNode.Id, relationType, label ?? relationType, weight, direction, relationshipId, srcNode.DisplayName, tgtNode.DisplayName, ct);
        }

        if (!string.IsNullOrEmpty(sourceCharName) && !string.IsNullOrEmpty(targetCharName))
        {
            var srcNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.GraphId == graphId && n.DisplayName == sourceCharName, ct);
            var tgtNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.GraphId == graphId && n.DisplayName == targetCharName, ct);

            if (srcNode == null) return ToolResult.Fail($"图谱中未找到角色节点「{sourceCharName}」，请先用 create_character_graph_node 添加", "source_not_in_graph");
            if (tgtNode == null) return ToolResult.Fail($"图谱中未找到角色节点「{targetCharName}」，请先用 create_character_graph_node 添加", "target_not_in_graph");

            return await CreateEdge(db, idGen, graphId, workId, srcNode.Id, tgtNode.Id, relationType, label ?? relationType, weight, direction, relationshipId, sourceCharName, targetCharName, ct);
        }

        return ToolResult.Fail("请使用策略A（source_node_id + target_node_id）或策略B（source_character_name + target_character_name）", "missing_node_ref");
    }

    private static async Task<ToolResult> CreateEdge(
        SpeakEaseDbContext db, ISnowflakeIdGenerator idGen,
        string graphId, string workId,
        string sourceNodeId, string targetNodeId,
        string relationType, string label, int weight, string direction,
        string relationshipId,
        string sourceName, string targetName,
        CancellationToken ct)
    {
        var existing = await db.CharacterGraphEdges.AsNoTracking()
            .FirstOrDefaultAsync(e => e.GraphId == graphId &&
                                      e.SourceNodeId == sourceNodeId &&
                                      e.TargetNodeId == targetNodeId &&
                                      e.RelationType == relationType, ct);

        if (existing != null)
            return ToolResult.Fail($"图谱中已存在相同关系边「{sourceName} →[{relationType}]→ {targetName}」，边ID: {existing.Id}", "edge_exists");

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
