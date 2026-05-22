using System.Text.Json;
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        CharacterEntity character = null;
        if (!string.IsNullOrEmpty(args.Id))
            character = await db.Characters.FirstOrDefaultAsync(c => c.Id == args.Id && c.WorkId == args.WorkId, ct);
        if (character == null)
            character = await db.Characters.FirstOrDefaultAsync(c => c.WorkId == args.WorkId && c.Name == args.Name, ct);

        if (character != null)
        {
            if (!string.IsNullOrEmpty(args.CoreSeed)) character.Identity = args.CoreSeed;
            if (!string.IsNullOrEmpty(args.Alias)) character.Alias = args.Alias;
            if (!string.IsNullOrEmpty(args.Gender)) character.Gender = args.Gender;
            if (!string.IsNullOrEmpty(args.AgeDescription)) character.AgeDescription = args.AgeDescription;
            if (args.Appearance != null) character.Appearance = args.Appearance;
            if (args.Motivation != null) character.Motivation = args.Motivation;
            if (args.BackgroundStory != null) character.BackgroundStory = args.BackgroundStory;
            if (args.Personality != null) character.Personality = args.Personality;
            if (!string.IsNullOrEmpty(args.AbilityDescription)) character.AbilityDescription = args.AbilityDescription;
            if (args.Tags != null && args.Tags.Count > 0) character.Tags = args.Tags;
            character.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"角色「{args.Name}」已更新，ID: {character.Id}");
        }

        var entity = new CharacterEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            Name = args.Name,
            Alias = args.Alias ?? string.Empty,
            Gender = args.Gender ?? string.Empty,
            AgeDescription = args.AgeDescription ?? string.Empty,
            Identity = args.CoreSeed ?? string.Empty,
            Appearance = args.Appearance,
            Motivation = args.Motivation,
            BackgroundStory = args.BackgroundStory,
            Personality = args.Personality,
            AbilityDescription = args.AbilityDescription ?? string.Empty,
            Tags = args.Tags ?? []
        };

        await db.Characters.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色「{args.Name}」已创建，ID: {entity.Id}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string Name { get; init; }
        public string CoreSeed { get; init; }
        public string Alias { get; init; }
        public string Gender { get; init; }
        public string AgeDescription { get; init; }
        public string Appearance { get; init; }
        public string Motivation { get; init; }
        public string BackgroundStory { get; init; }
        public string Personality { get; init; }
        public string AbilityDescription { get; init; }
        public List<string> Tags { get; init; }

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
