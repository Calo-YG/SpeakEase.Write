using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateFactionTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_faction",
            Description = "创建势力条目（门派/家族/国家/组织），用于世界观构建。faction_type 建议: 宗门/家族/帝国/商会/佣兵团/暗组织。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["name"] = new() { Type = "string", Description = "势力名称（必填）" },
                    ["faction_type"] = new() { Type = "string", Description = "势力类型（必填），如: 宗门/家族/帝国/商会/佣兵团/暗组织" },
                    ["description"] = new() { Type = "string", Description = "势力描述（必填），包含历史、实力、特点等" }
                },
                Required = ["work_id", "name", "faction_type", "description"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var name = args.GetString("name", required: true);
        var factionType = args.GetString("faction_type", required: true);
        var description = args.GetString("description", required: true);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == workId, ct);

        var entity = new FactionEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Name = name,
            FactionType = factionType,
            Description = description
        };

        await db.Factions.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"势力「{name}」（{factionType}）已创建，ID: {entity.Id}");
    }
}
