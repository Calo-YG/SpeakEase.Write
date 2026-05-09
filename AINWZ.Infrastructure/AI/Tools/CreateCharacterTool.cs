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
            Description = "创建新角色。必填 work_id + name + coreSeed（身份描述）。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["name"] = new() { Type = "string", Description = "角色名称（必填）" },
                    ["coreSeed"] = new() { Type = "string", Description = "身份/核心种子（必填），简要描述角色在故事中的身份" },
                    ["appearance"] = new() { Type = "string", Description = "外貌特征（可选）" },
                    ["motivation"] = new() { Type = "string", Description = "角色动机（可选）" },
                    ["backgroundStory"] = new() { Type = "string", Description = "背景故事（可选）" },
                    ["personality"] = new() { Type = "string", Description = "性格描述（可选）" }
                },
                Required = ["work_id", "name", "coreSeed"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var name = args.GetString("name", required: true);
        var coreSeed = args.GetString("coreSeed", required: true);
        var appearance = args.GetString("appearance");
        var motivation = args.GetString("motivation");
        var backgroundStory = args.GetString("backgroundStory");
        var personality = args.GetString("personality");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var character = new CharacterEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            Name = name,
            Identity = coreSeed,
            Appearance = appearance,
            Motivation = motivation,
            BackgroundStory = backgroundStory,
            Personality = personality
        };

        await db.Characters.AddAsync(character, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色「{name}」已创建，ID: {character.Id}，身份: {coreSeed}");
    }
}
