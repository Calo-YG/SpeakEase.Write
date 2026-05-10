using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreatePowerSystemTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_power_system",
            Description = "创建力量体系/修炼体系条目，用于世界观构建。可存储修仙等级、武功境界、魔法体系等结构化定义。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["name"] = new() { Type = "string", Description = "体系名称（必填），如: 修仙境界、武道等级" },
                    ["level_definition"] = new() { Type = "string", Description = "等级定义（必填），JSON格式，如 {\"levels\":[\"炼气\",\"筑基\",\"金丹\",\"元婴\"]}" },
                    ["ability_rule"] = new() { Type = "string", Description = "能力规则（可选），如: 金丹期可御剑飞行" },
                    ["resource_system"] = new() { Type = "string", Description = "资源体系（可选），如: 灵石为通用货币" }
                },
                Required = ["work_id", "name", "level_definition"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var name = args.GetString("name", required: true);
        var levelDefinition = args.GetString("level_definition", required: true);
        var abilityRule = args.GetString("ability_rule");
        var resourceSystem = args.GetString("resource_system");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == workId, ct);

        var entity = new PowerSystemEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Name = name,
            LevelDefinitionJson = levelDefinition,
            AbilityRule = abilityRule ?? string.Empty,
            ResourceSystem = resourceSystem ?? string.Empty
        };

        await db.PowerSystems.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"力量体系「{name}」已创建，ID: {entity.Id}");
    }
}
