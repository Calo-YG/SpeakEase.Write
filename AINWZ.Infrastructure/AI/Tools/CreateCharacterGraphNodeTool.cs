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
            Description = "向角色关系图谱中添加角色节点。通过角色名称或ID关联已存在的角色到图谱中，可指定位置和重要程度",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["graph_id"] = new() { Type = "string", Description = "图谱ID（必填），从 create_character_graph 或 get_character_graph 获取" },
                    ["character_name"] = new() { Type = "string", Description = "角色名称（二选一，与 character_id 至少提供一个），按名称匹配角色" },
                    ["character_id"] = new() { Type = "string", Description = "角色ID（二选一，与 character_name 至少提供一个），直接按ID关联角色" },
                    ["node_type"] = new() { Type = "string", Description = "节点类型（可选），如: protagonist/antagonist/supporting/minor，默认 supporting" },
                    ["importance"] = new() { Type = "integer", Description = "重要程度（可选，1-10，默认5），10为绝对核心" },
                    ["x"] = new() { Type = "number", Description = "X坐标（可选），前端布局横坐标" },
                    ["y"] = new() { Type = "number", Description = "Y坐标（可选），前端布局纵坐标" },
                    ["style_json"] = new() { Type = "string", Description = "节点样式JSON（可选），用于自定义节点外观" }
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
        var characterName = args.GetString("character_name");
        var characterId = args.GetString("character_id");
        var nodeType = args.GetString("node_type") ?? "supporting";
        var importance = args.GetInt32("importance", defaultValue: 5, min: 1, max: 10);
        var styleJson = args.GetString("style_json");
        if (args.HasErrors) return args.ToErrorResult();

        if (string.IsNullOrEmpty(characterName) && string.IsNullOrEmpty(characterId))
            return ToolResult.Fail("character_name 和 character_id 至少提供一个", "missing_character_ref");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var graph = await db.CharacterGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == graphId && x.WorkId == workId, ct);

        if (graph == null)
            return ToolResult.Fail($"未找到图谱 {graphId}", "graph_not_found");

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

        var existingNode = await db.CharacterGraphNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.GraphId == graphId && n.CharacterId == character.Id, ct);

        if (existingNode != null)
            return ToolResult.Fail($"角色「{character.Name}」已存在于图谱中，节点ID: {existingNode.Id}", "node_exists");

        var node = new CharacterGraphNodeEntity
        {
            Id = idGen.NextIdString(),
            GraphId = graphId,
            WorkId = workId,
            CharacterId = character.Id,
            DisplayName = character.Name ?? character.Id,
            NodeType = nodeType,
            Importance = importance,
            StyleJson = styleJson ?? string.Empty
        };

        await db.CharacterGraphNodes.AddAsync(node, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色节点「{node.DisplayName}」已添加到图谱，节点ID: {node.Id}，类型: {nodeType}，重要度: {importance}");
    }
}
