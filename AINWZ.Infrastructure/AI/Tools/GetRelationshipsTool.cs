using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 角色关系查询工具：按角色名称查询其完整的人际关系网络，返回关联角色名、关系类型和强度
public sealed class GetRelationshipsTool(IServiceScopeFactory scopeFactory, IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_relationships",
            Description = "查询指定角色的人际关系网络",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["character_name"] = new() { Type = "string", Description = "角色名称（必填）" }
                },
                Required = ["work_id", "character_name"]
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

        var character = await db.Characters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.WorkId == args.WorkId && c.Name == args.CharacterName, ct)
            ?? await db.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.WorkId == args.WorkId && c.Name != null && c.Name.Contains(args.CharacterName), ct);

        if (character == null)
            return ToolResult.Fail($"未找到角色「{args.CharacterName}」", "character_not_found");

        var relationships = await db.CharacterRelationships.AsNoTracking()
            .Where(r => r.WorkId == args.WorkId && (r.SourceCharacterId == character.Id || r.TargetCharacterId == character.Id))
            .ToListAsync(ct);

        var relatedIds = relationships
            .SelectMany(r => new[] { r.SourceCharacterId, r.TargetCharacterId })
            .Where(id => id != character.Id && !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var relatedNames = new Dictionary<string, string>();
        if (relatedIds.Count > 0)
        {
            var chars = await db.Characters.AsNoTracking()
                .Where(c => relatedIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(ct);
            foreach (var c in chars)
                relatedNames[c.Id] = c.Name ?? c.Id;
        }

        var relList = new List<object>();
        foreach (var r in relationships)
        {
            var otherId = r.SourceCharacterId == character.Id ? r.TargetCharacterId : r.SourceCharacterId;
            var otherName = relatedNames.GetValueOrDefault(otherId ?? string.Empty, otherId ?? "未知");
            relList.Add(new
            {
                Target = otherName,
                r.RelationshipType,
                r.Description,
                r.Intensity
            });
        }

        return ToolResult.Ok(JsonSerializer.Serialize(new
        {
            character.Name,
            Relationships = relList
        }, snapshot.Value));
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string CharacterName { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(CharacterName))
                return ToolResult.Fail("缺少必需参数 'character_name'", "argument_parse_error");
            return null;
        }
    }
}
