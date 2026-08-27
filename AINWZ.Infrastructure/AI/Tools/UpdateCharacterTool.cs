using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 角色更新工具：按 work_id+name 精确匹配角色，至少更新一个字段
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        if (string.IsNullOrEmpty(args.Personality) && string.IsNullOrEmpty(args.Appearance) &&
            string.IsNullOrEmpty(args.Motivation) && string.IsNullOrEmpty(args.BackgroundStory) &&
            string.IsNullOrEmpty(args.CoreSeed) && string.IsNullOrEmpty(args.Alias) &&
            string.IsNullOrEmpty(args.Gender) && string.IsNullOrEmpty(args.AgeDescription) &&
            string.IsNullOrEmpty(args.AbilityDescription) && (args.Tags == null || args.Tags.Count == 0))
            return ToolResult.Fail("至少需要提供一个更新字段", "no_fields");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

        var character = await db.Characters.FirstOrDefaultAsync(
            c => c.WorkId == args.WorkId && c.Name == args.Name, ct);

        if (character == null)
            return ToolResult.Fail($"未找到角色「{args.Name}」，请确认角色名称和作品ID", "not_found");

        if (!string.IsNullOrEmpty(args.Alias)) character.Alias = args.Alias;
        if (!string.IsNullOrEmpty(args.Gender)) character.Gender = args.Gender;
        if (!string.IsNullOrEmpty(args.AgeDescription)) character.AgeDescription = args.AgeDescription;
        if (!string.IsNullOrEmpty(args.Personality)) character.Personality = args.Personality;
        if (!string.IsNullOrEmpty(args.Appearance)) character.Appearance = args.Appearance;
        if (!string.IsNullOrEmpty(args.Motivation)) character.Motivation = args.Motivation;
        if (!string.IsNullOrEmpty(args.BackgroundStory)) character.BackgroundStory = args.BackgroundStory;
        if (!string.IsNullOrEmpty(args.CoreSeed)) character.Identity = args.CoreSeed;
        if (!string.IsNullOrEmpty(args.AbilityDescription)) character.AbilityDescription = args.AbilityDescription;
        if (args.Tags != null && args.Tags.Count > 0) character.Tags = args.Tags;

        character.UpdateAt = DateTime.Now;
        await db.SaveChangesAsync(ct);
        return ToolResult.Ok($"角色「{args.Name}」已更新");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Name { get; init; }
        public string Alias { get; init; }
        public string Gender { get; init; }
        public string AgeDescription { get; init; }
        public string Personality { get; init; }
        public string Appearance { get; init; }
        public string Motivation { get; init; }
        public string BackgroundStory { get; init; }
        public string CoreSeed { get; init; }
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
