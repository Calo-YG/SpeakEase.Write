using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class UpdateCharacterTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "update_character",
            Description = "更新已有角色的设定信息，按 work_id + name 精确匹配。至少需要一个更新字段。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["name"] = new() { Type = "string", Description = "角色名称（必填），需与已有角色精确匹配" },
                    ["alias"] = new() { Type = "string", Description = "角色别名/外号（可选）" },
                    ["gender"] = new() { Type = "string", Description = "性别描述（可选）" },
                    ["ageDescription"] = new() { Type = "string", Description = "年龄描述（可选）" },
                    ["personality"] = new() { Type = "string", Description = "性格描述（可选）" },
                    ["appearance"] = new() { Type = "string", Description = "外貌特征（可选）" },
                    ["motivation"] = new() { Type = "string", Description = "角色动机（可选）" },
                    ["background_story"] = new() { Type = "string", Description = "背景故事（可选）" },
                    ["coreSeed"] = new() { Type = "string", Description = "身份/核心种子（可选）" },
                    ["abilityDescription"] = new() { Type = "string", Description = "能力/武功/技能描述（可选）" },
                    ["tags"] = new() { Type = "array", Items = new ParameterSchema { Type = "string" }, Description = "角色标签（可选），完全替换已有标签" }
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
        var alias = args.GetString("alias");
        var gender = args.GetString("gender");
        var ageDescription = args.GetString("ageDescription");
        var personality = args.GetString("personality");
        var appearance = args.GetString("appearance");
        var motivation = args.GetString("motivation");
        var backgroundStory = args.GetString("background_story");
        var coreSeed = args.GetString("coreSeed");
        var abilityDescription = args.GetString("abilityDescription");
        var tags = args.GetStringArray("tags");
        if (args.HasErrors) return args.ToErrorResult();

        if (string.IsNullOrEmpty(personality) && string.IsNullOrEmpty(appearance) &&
            string.IsNullOrEmpty(motivation) && string.IsNullOrEmpty(backgroundStory) &&
            string.IsNullOrEmpty(coreSeed) && string.IsNullOrEmpty(alias) &&
            string.IsNullOrEmpty(gender) && string.IsNullOrEmpty(ageDescription) &&
            string.IsNullOrEmpty(abilityDescription) && tags.Count == 0)
            return ToolResult.Fail("至少需要提供一个更新字段", "no_fields");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var character = await db.Characters.FirstOrDefaultAsync(
            c => c.WorkId == workId && c.Name == name, ct);

        if (character == null)
            return ToolResult.Fail($"未找到角色「{name}」，请确认角色名称和作品ID", "not_found");

        if (!string.IsNullOrEmpty(alias)) character.Alias = alias;
        if (!string.IsNullOrEmpty(gender)) character.Gender = gender;
        if (!string.IsNullOrEmpty(ageDescription)) character.AgeDescription = ageDescription;
        if (!string.IsNullOrEmpty(personality)) character.Personality = personality;
        if (!string.IsNullOrEmpty(appearance)) character.Appearance = appearance;
        if (!string.IsNullOrEmpty(motivation)) character.Motivation = motivation;
        if (!string.IsNullOrEmpty(backgroundStory)) character.BackgroundStory = backgroundStory;
        if (!string.IsNullOrEmpty(coreSeed)) character.Identity = coreSeed;
        if (!string.IsNullOrEmpty(abilityDescription)) character.AbilityDescription = abilityDescription;
        if (tags.Count > 0) character.Tags = tags;

        character.UpdateAt = DateTime.Now;
        await db.SaveChangesAsync(ct);
        return ToolResult.Ok($"角色「{name}」已更新");
    }
}
