using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
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
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var name = args.GetString("name", required: true);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var character = await db.Characters.AsNoTracking()
            .Where(c => c.WorkId == workId && c.Name != null && c.Name.Contains(name))
            .OrderBy(c => c.Name == name ? 0 : 1)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Identity,
                c.BackgroundStory,
                c.Personality,
                c.Gender,
                c.Appearance,
                c.Motivation
            })
            .FirstOrDefaultAsync(ct);

        if (character == null)
            return ToolResult.Fail($"未找到角色「{name}」", "not_found");

        var relationships = await db.CharacterRelationships.AsNoTracking()
            .Where(r => r.WorkId == workId && (r.SourceCharacterId == character.Id || r.TargetCharacterId == character.Id))
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
        sb.AppendLine($"身份：{character.Identity ?? "未设置"}");
        sb.AppendLine($"性别：{character.Gender ?? "未设置"}");

        if (!string.IsNullOrEmpty(character.Appearance))
            sb.AppendLine($"外貌：{character.Appearance}");

        if (!string.IsNullOrEmpty(character.BackgroundStory))
            sb.AppendLine($"背景故事：{character.BackgroundStory}");

        if (!string.IsNullOrEmpty(character.Personality))
            sb.AppendLine($"性格：{character.Personality}");

        if (!string.IsNullOrEmpty(character.Motivation))
            sb.AppendLine($"动机：{character.Motivation}");

        if (relationships.Count > 0)
        {
            sb.AppendLine("人物关系：");
            foreach (var rel in relationships)
            {
                var otherId = rel.SourceCharacterId == character.Id ? rel.TargetCharacterId : rel.SourceCharacterId;
                var otherName = relatedCharacters.GetValueOrDefault(otherId ?? string.Empty, otherId ?? "未知");
                sb.AppendLine($"  与{otherName}：{rel.RelationshipType ?? "未知"} — {rel.Description ?? "无描述"}");
            }
        }

        return ToolResult.Ok(sb.ToString());
    }
}
