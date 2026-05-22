using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetCharacterTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_character",
            Description = "按姓名查询角色的完整设定，包含人物关系、背景故事、性格。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["name"] = new() { Type = "string", Description = "角色姓名（必填，支持模糊匹配）" }
                },
                Required = ["work_id", "name"]
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
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var character = await db.Characters.AsNoTracking()
            .Where(c => c.WorkId == args.WorkId && c.Name != null && c.Name.Contains(args.Name))
            .OrderBy(c => c.Name == args.Name ? 0 : 1)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Alias,
                c.Gender,
                c.AgeDescription,
                c.Identity,
                c.BackgroundStory,
                c.Personality,
                c.Appearance,
                c.Motivation,
                c.AbilityDescription,
                c.Tags
            })
            .FirstOrDefaultAsync(ct);

        if (character == null)
            return ToolResult.Fail($"未找到角色「{args.Name}」", "not_found");

        var relationships = await db.CharacterRelationships.AsNoTracking()
            .Where(r => r.WorkId == args.WorkId && (r.SourceCharacterId == character.Id || r.TargetCharacterId == character.Id))
            .Take(100)
            .ToListAsync(ct);

        var characterIds = relationships
            .SelectMany(r => new[] { r.SourceCharacterId, r.TargetCharacterId })
            .Where(id => id != character.Id && !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var relatedCharacters = new Dictionary<string, string>();
        if (characterIds.Count > 0)
        {
            var chars = await db.Characters.AsNoTracking()
                .Where(c => characterIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(ct);
            foreach (var c in chars)
                relatedCharacters[c.Id] = c.Name ?? c.Id;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"角色：{character.Name}");
        if (!string.IsNullOrEmpty(character.Alias))
            sb.AppendLine($"别名：{character.Alias}");
        sb.AppendLine($"身份：{character.Identity ?? "未设置"}");
        sb.AppendLine($"性别：{character.Gender ?? "未设置"}");
        if (!string.IsNullOrEmpty(character.AgeDescription))
            sb.AppendLine($"年龄：{character.AgeDescription}");

        if (!string.IsNullOrEmpty(character.Appearance))
            sb.AppendLine($"外貌：{character.Appearance}");

        if (!string.IsNullOrEmpty(character.BackgroundStory))
            sb.AppendLine($"背景故事：{character.BackgroundStory}");

        if (!string.IsNullOrEmpty(character.Personality))
            sb.AppendLine($"性格：{character.Personality}");

        if (!string.IsNullOrEmpty(character.Motivation))
            sb.AppendLine($"动机：{character.Motivation}");

        if (!string.IsNullOrEmpty(character.AbilityDescription))
            sb.AppendLine($"能力：{character.AbilityDescription}");

        if (character.Tags is { Count: > 0 })
            sb.AppendLine($"标签：{string.Join("、", character.Tags)}");

        if (relationships.Count > 0)
        {
            sb.AppendLine("人物关系：");
            foreach (var rel in relationships)
            {
                var otherId = rel.SourceCharacterId == character.Id ? rel.TargetCharacterId : rel.SourceCharacterId;
                var otherName = relatedCharacters.GetValueOrDefault(otherId ?? string.Empty, otherId ?? "未知");
                sb.AppendLine($"  与{otherName}：{rel.RelationshipType ?? "未知"}（强度：{rel.Intensity}）— {rel.Description ?? "无描述"}");
            }
        }

        return ToolResult.Ok(sb.ToString());
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Name { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Name))
                return ToolResult.Fail("缺少必需参数 'name'", "argument_parse_error");
            return null;
        }
    }
}
