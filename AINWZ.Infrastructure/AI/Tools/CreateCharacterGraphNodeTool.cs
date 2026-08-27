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

// 角色图谱节点工具：向关系图谱添加/更新角色节点，按角色名称或ID关联已有角色
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var graph = await db.CharacterGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == args.GraphId && x.WorkId == args.WorkId, ct);
        if (graph == null)
            return ToolResult.Fail($"未找到图谱 {args.GraphId}", "graph_not_found");

        CharacterGraphNodeEntity existingNode = null;
        if (!string.IsNullOrEmpty(args.Id))
            existingNode = await db.CharacterGraphNodes.FirstOrDefaultAsync(
                n => n.Id == args.Id &&
                     n.WorkId == args.WorkId &&
                     n.GraphId == args.GraphId,
                ct);

        if (existingNode == null && !string.IsNullOrEmpty(args.CharacterId))
            existingNode = await db.CharacterGraphNodes.FirstOrDefaultAsync(
                n => n.WorkId == args.WorkId &&
                     n.GraphId == args.GraphId &&
                     n.CharacterId == args.CharacterId,
                ct);

        if (existingNode == null && !string.IsNullOrEmpty(args.CharacterName))
        {
            var ch = await db.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.WorkId == args.WorkId && c.Name == args.CharacterName, ct);
            if (ch != null)
                existingNode = await db.CharacterGraphNodes.FirstOrDefaultAsync(
                    n => n.WorkId == args.WorkId &&
                         n.GraphId == args.GraphId &&
                         n.CharacterId == ch.Id,
                    ct);
        }

        if (existingNode != null)
        {
            if (!string.IsNullOrEmpty(args.NodeType)) existingNode.NodeType = args.NodeType;
            if (args.Importance > 0) existingNode.Importance = args.Importance;
            if (args.StyleJson != null) existingNode.StyleJson = args.StyleJson ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"角色节点「{existingNode.DisplayName}」已更新，类型: {existingNode.NodeType}，重要度: {existingNode.Importance}");
        }

        if (string.IsNullOrEmpty(args.CharacterName) && string.IsNullOrEmpty(args.CharacterId))
            return ToolResult.Fail("character_name 和 character_id 至少提供一个", "missing_character_ref");

        CharacterEntity character;
        if (!string.IsNullOrEmpty(args.CharacterId))
        {
            character = await db.Characters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == args.CharacterId && c.WorkId == args.WorkId, ct);
        }
        else
        {
            character = await db.Characters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.WorkId == args.WorkId && c.Name == args.CharacterName, ct);
        }

        if (character == null)
            return ToolResult.Fail($"未找到角色「{args.CharacterName ?? args.CharacterId}」", "character_not_found");

        var node = new CharacterGraphNodeEntity
        {
            Id = idGen.NextIdString(),
            GraphId = args.GraphId,
            WorkId = args.WorkId,
            CharacterId = character.Id,
            DisplayName = character.Name ?? character.Id,
            NodeType = args.NodeType ?? "supporting",
            Importance = args.Importance > 0 ? args.Importance : 5,
            StyleJson = args.StyleJson ?? string.Empty
        };

        await db.CharacterGraphNodes.AddAsync(node, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Detach(node);
            existingNode = await db.CharacterGraphNodes.FirstOrDefaultAsync(
                n => n.WorkId == args.WorkId &&
                     n.GraphId == args.GraphId &&
                     n.CharacterId == character.Id,
                ct);
            if (existingNode == null)
                throw;

            if (!string.IsNullOrEmpty(args.NodeType)) existingNode.NodeType = args.NodeType;
            if (args.Importance > 0) existingNode.Importance = args.Importance;
            if (args.StyleJson != null) existingNode.StyleJson = args.StyleJson ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"角色节点「{existingNode.DisplayName}」已更新，类型: {existingNode.NodeType}，重要度: {existingNode.Importance}");
        }

        return ToolResult.Ok($"角色节点「{node.DisplayName}」已添加到图谱，节点ID: {node.Id}，类型: {node.NodeType}，重要度: {node.Importance}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string GraphId { get; init; }
        public string Id { get; init; }
        public string CharacterName { get; init; }
        public string CharacterId { get; init; }
        public string NodeType { get; init; }
        public int Importance { get; init; }
        public string StyleJson { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(GraphId))
                return ToolResult.Fail("缺少必需参数 'graph_id'", "argument_parse_error");
            if (Importance != 0 && (Importance < 1 || Importance > 10))
                return ToolResult.Fail($"参数 'importance' 值 {Importance} 超出范围 [1, 10]", "argument_parse_error");
            return null;
        }
    }
}
