using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 角色图谱边工具：创建/更新图谱中两个节点之间的关系连线，支持节点ID和角色名称两种指定策略
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICharacterDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var graph = await db.CharacterGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == args.GraphId && x.WorkId == args.WorkId, ct);
        if (graph == null)
            return ToolResult.Fail($"未找到图谱 {args.GraphId}", "graph_not_found");

        if (!string.IsNullOrEmpty(args.Id))
        {
            var existing = await db.CharacterGraphEdges.FirstOrDefaultAsync(
                e => e.Id == args.Id &&
                     e.WorkId == args.WorkId &&
                     e.GraphId == args.GraphId,
                ct);
            if (existing == null)
                return ToolResult.Fail($"未找到边 {args.Id}", "edge_not_found");

            if (!string.IsNullOrEmpty(args.RelationType)) existing.RelationType = args.RelationType;
            if (args.Label != null) existing.Label = args.Label ?? string.Empty;
            if (args.Weight > 0) existing.Weight = args.Weight;
            if (args.Direction != null) existing.Direction = args.Direction ?? "directed";
            if (args.RelationshipId != null) existing.RelationshipId = args.RelationshipId ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"边已更新，ID: {existing.Id}");
        }

        if (!string.IsNullOrEmpty(args.SourceNodeId) && !string.IsNullOrEmpty(args.TargetNodeId))
        {
            var srcNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.Id == args.SourceNodeId &&
                         n.WorkId == args.WorkId &&
                         n.GraphId == args.GraphId,
                    ct);
            var tgtNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.Id == args.TargetNodeId &&
                         n.WorkId == args.WorkId &&
                         n.GraphId == args.GraphId,
                    ct);
            if (srcNode == null) return ToolResult.Fail($"未找到源节点 {args.SourceNodeId}", "source_node_not_found");
            if (tgtNode == null) return ToolResult.Fail($"未找到目标节点 {args.TargetNodeId}", "target_node_not_found");
            return await CreateOrUpdateEdge(db, idGen, args.GraphId, args.WorkId, srcNode.Id, tgtNode.Id, args.RelationType ?? "unknown", args.Label ?? args.RelationType ?? "unknown", args.Weight > 0 ? args.Weight : 5, args.Direction ?? "directed", args.RelationshipId, srcNode.DisplayName, tgtNode.DisplayName, ct);
        }

        if (!string.IsNullOrEmpty(args.SourceCharacterName) && !string.IsNullOrEmpty(args.TargetCharacterName))
        {
            var srcNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.WorkId == args.WorkId &&
                         n.GraphId == args.GraphId &&
                         n.DisplayName == args.SourceCharacterName,
                    ct);
            var tgtNode = await db.CharacterGraphNodes.AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.WorkId == args.WorkId &&
                         n.GraphId == args.GraphId &&
                         n.DisplayName == args.TargetCharacterName,
                    ct);
            if (srcNode == null) return ToolResult.Fail($"图谱中未找到角色节点「{args.SourceCharacterName}」", "source_not_in_graph");
            if (tgtNode == null) return ToolResult.Fail($"图谱中未找到角色节点「{args.TargetCharacterName}」", "target_not_in_graph");
            return await CreateOrUpdateEdge(db, idGen, args.GraphId, args.WorkId, srcNode.Id, tgtNode.Id, args.RelationType ?? "unknown", args.Label ?? args.RelationType ?? "unknown", args.Weight > 0 ? args.Weight : 5, args.Direction ?? "directed", args.RelationshipId, args.SourceCharacterName, args.TargetCharacterName, ct);
        }

        return ToolResult.Fail("新建边时请提供策略A（source_node_id+target_node_id）或策略B（source_character_name+target_character_name），更新时请提供 id", "missing_node_ref");
    }

    private static async Task<ToolResult> CreateOrUpdateEdge(
        ICharacterDbContext db, ISnowflakeIdGenerator idGen,
        string graphId, string workId,
        string sourceNodeId, string targetNodeId,
        string relationType, string label, int weight, string direction,
        string relationshipId,
        string sourceName, string targetName,
        CancellationToken ct)
    {
        var existing = await db.CharacterGraphEdges
            .FirstOrDefaultAsync(e => e.WorkId == workId &&
                                      e.GraphId == graphId &&
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
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Detach(edge);
            existing = await db.CharacterGraphEdges.FirstOrDefaultAsync(
                e => e.WorkId == workId &&
                     e.GraphId == graphId &&
                     e.SourceNodeId == sourceNodeId &&
                     e.TargetNodeId == targetNodeId,
                ct);
            if (existing == null)
                throw;

            existing.RelationType = relationType;
            existing.Label = label;
            existing.Weight = weight;
            if (!string.IsNullOrEmpty(direction)) existing.Direction = direction;
            if (!string.IsNullOrEmpty(relationshipId)) existing.RelationshipId = relationshipId;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"边已更新: {sourceName} →[{relationType}]→ {targetName}，权重: {weight}");
        }

        return ToolResult.Ok($"边已创建: {sourceName} →[{relationType}]→ {targetName}，边ID: {edge.Id}，权重: {weight}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string GraphId { get; init; }
        public string Id { get; init; }
        public string SourceNodeId { get; init; }
        public string TargetNodeId { get; init; }
        public string SourceCharacterName { get; init; }
        public string TargetCharacterName { get; init; }
        public string RelationType { get; init; }
        public string Label { get; init; }
        public int Weight { get; init; }
        public string Direction { get; init; }
        public string RelationshipId { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(GraphId))
                return ToolResult.Fail("缺少必需参数 'graph_id'", "argument_parse_error");
            if (Weight != 0 && (Weight < 1 || Weight > 10))
                return ToolResult.Fail($"参数 'weight' 值 {Weight} 超出范围 [1, 10]", "argument_parse_error");
            return null;
        }
    }
}
