using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateCharacterTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_character",
            Description = "创建新角色或更新已有角色。按 id 或 name 查找已有角色，存在则更新，不存在则创建。必填 work_id + name。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["id"] = new() { Type = "string", Description = "角色ID（可选），用于更新已有角色" },
                    ["name"] = new() { Type = "string", Description = "角色名称（必填）" },
                    ["coreSeed"] = new() { Type = "string", Description = "身份/核心种子（新建必填，更新可选）" },
                    ["alias"] = new() { Type = "string", Description = "角色别名/外号（可选）" },
                    ["gender"] = new() { Type = "string", Description = "性别描述（可选）" },
                    ["ageDescription"] = new() { Type = "string", Description = "年龄描述（可选）" },
                    ["appearance"] = new() { Type = "string", Description = "外貌特征（可选）" },
                    ["motivation"] = new() { Type = "string", Description = "角色动机（可选）" },
                    ["backgroundStory"] = new() { Type = "string", Description = "背景故事（可选）" },
                    ["personality"] = new() { Type = "string", Description = "性格描述（可选）" },
                    ["abilityDescription"] = new() { Type = "string", Description = "能力/武功/技能描述（可选）" },
                    ["tags"] = new() { Type = "array", Items = new ParameterSchema { Type = "string" }, Description = "角色标签（可选）" }
                },
                Required = ["work_id", "name"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var id = args.GetString("id");
        var name = args.GetString("name", required: true);
        var coreSeed = args.GetString("coreSeed");
        var alias = args.GetString("alias");
        var gender = args.GetString("gender");
        var ageDescription = args.GetString("ageDescription");
        var appearance = args.GetString("appearance");
        var motivation = args.GetString("motivation");
        var backgroundStory = args.GetString("backgroundStory");
        var personality = args.GetString("personality");
        var abilityDescription = args.GetString("abilityDescription");
        var tags = args.GetStringArray("tags");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        CharacterEntity character = null;
        if (!string.IsNullOrEmpty(id))
            character = await db.Characters.FirstOrDefaultAsync(c => c.Id == id && c.WorkId == workId, ct);
        if (character == null)
            character = await db.Characters.FirstOrDefaultAsync(c => c.WorkId == workId && c.Name == name, ct);

        if (character != null)
        {
            if (!string.IsNullOrEmpty(coreSeed)) character.Identity = coreSeed;
            if (!string.IsNullOrEmpty(alias)) character.Alias = alias;
            if (!string.IsNullOrEmpty(gender)) character.Gender = gender;
            if (!string.IsNullOrEmpty(ageDescription)) character.AgeDescription = ageDescription;
            if (appearance != null) character.Appearance = appearance;
            if (motivation != null) character.Motivation = motivation;
            if (backgroundStory != null) character.BackgroundStory = backgroundStory;
            if (personality != null) character.Personality = personality;
            if (!string.IsNullOrEmpty(abilityDescription)) character.AbilityDescription = abilityDescription;
            if (tags.Count > 0) character.Tags = tags;
            character.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"角色「{name}」已更新，ID: {character.Id}");
        }

        var entity = new CharacterEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            Name = name,
            Alias = alias ?? string.Empty,
            Gender = gender ?? string.Empty,
            AgeDescription = ageDescription ?? string.Empty,
            Identity = coreSeed ?? string.Empty,
            Appearance = appearance,
            Motivation = motivation,
            BackgroundStory = backgroundStory,
            Personality = personality,
            AbilityDescription = abilityDescription ?? string.Empty,
            Tags = tags ?? []
        };

        await db.Characters.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色「{name}」已创建，ID: {entity.Id}");
    }
}
