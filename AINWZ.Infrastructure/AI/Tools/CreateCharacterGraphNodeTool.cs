using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateCharacterGraphNodeTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_character_graph_node",
            Description = "向角色关系图谱中添加或更新角色节点。通过角色名称或ID关联已存在的角色到图谱中，可按 character_id 查找已有节点并更新node_type/importance/style_json等属性。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["graph_id"] = new() { Type = "string", Description = "图谱ID（必填）" },
                    ["id"] = new() { Type = "string", Description = "节点ID（可选），用于更新已有节点" },
                    ["character_name"] = new() { Type = "string", Description = "角色名称（二选一，与 character_id 至少提供一个）" },
                    ["character_id"] = new() { Type = "string", Description = "角色ID（二选一，与 character_name 至少提供一个）" },
                    ["node_type"] = new() { Type = "string", Description = "节点类型（可选），如: protagonist/antagonist/supporting/minor" },
                    ["importance"] = new() { Type = "integer", Description = "重要程度（可选，1-10）" },
                    ["style_json"] = new() { Type = "string", Description = "节点样式JSON（可选）" }
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
        var nodeId = args.GetString("id");
        var characterName = args.GetString("character_name");
        var characterId = args.GetString("character_id");
        var nodeType = args.GetString("node_type");
        var importance = args.GetInt32("importance", defaultValue: 0, min: 1, max: 10);
        var styleJson = args.GetString("style_json");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var graph = await db.CharacterGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == graphId && x.WorkId == workId, ct);
        if (graph == null)
            return ToolResult.Fail($"未找到图谱 {graphId}", "graph_not_found");

        CharacterGraphNodeEntity existingNode = null;
        if (!string.IsNullOrEmpty(nodeId))
            existingNode = await db.CharacterGraphNodes.FindAsync(nodeId, ct);

        if (existingNode == null && !string.IsNullOrEmpty(characterId))
            existingNode = await db.CharacterGraphNodes.FirstOrDefaultAsync(n => n.GraphId == graphId && n.CharacterId == characterId, ct);

        if (existingNode == null && !string.IsNullOrEmpty(characterName))
        {
            var ch = await db.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.WorkId == workId && c.Name == characterName, ct);
            if (ch != null)
                existingNode = await db.CharacterGraphNodes.FirstOrDefaultAsync(n => n.GraphId == graphId && n.CharacterId == ch.Id, ct);
        }

        if (existingNode != null)
        {
            if (!string.IsNullOrEmpty(nodeType)) existingNode.NodeType = nodeType;
            if (importance > 0) existingNode.Importance = importance;
            if (args.Has("style_json")) existingNode.StyleJson = styleJson ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"角色节点「{existingNode.DisplayName}」已更新，类型: {existingNode.NodeType}，重要度: {existingNode.Importance}");
        }

        if (string.IsNullOrEmpty(characterName) && string.IsNullOrEmpty(characterId))
            return ToolResult.Fail("character_name 和 character_id 至少提供一个", "missing_character_ref");

        CharacterEntity character;
        if (!string.IsNullOrEmpty(characterId))
        {
            character = await db.Characters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == characterId && c.WorkId == workId, ct);
        }
        else
        {
            character = await db.Characters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.WorkId == workId && c.Name == characterName, ct);
        }

        if (character == null)
            return ToolResult.Fail($"未找到角色「{characterName ?? characterId}」", "character_not_found");

        var node = new CharacterGraphNodeEntity
        {
            Id = idGen.NextIdString(),
            GraphId = graphId,
            WorkId = workId,
            CharacterId = character.Id,
            DisplayName = character.Name ?? character.Id,
            NodeType = nodeType ?? "supporting",
            Importance = importance > 0 ? importance : 5,
            StyleJson = styleJson ?? string.Empty
        };

        await db.CharacterGraphNodes.AddAsync(node, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色节点「{node.DisplayName}」已添加到图谱，节点ID: {node.Id}，类型: {node.NodeType}，重要度: {node.Importance}");
    }
}
