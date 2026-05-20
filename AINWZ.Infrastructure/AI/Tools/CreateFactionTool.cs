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
            Description = "创建或更新势力条目（门派/家族/国家/组织），用于世界观构建。通过 id 或 name 查找已有势力，存在则更新，不存在则创建。faction_type 建议: 宗门/家族/帝国/商会/佣兵团/暗组织。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "势力ID（可选），用于更新已有势力" },
                    ["name"] = new() { Type = "string", Description = "势力名称（必填）" },
                    ["faction_type"] = new() { Type = "string", Description = "势力类型（新建必填，更新可选），如: 宗门/家族/帝国/商会/佣兵团/暗组织" },
                    ["description"] = new() { Type = "string", Description = "势力描述（新建必填，更新可选），包含历史、实力、特点等" },
                    ["relationship_json"] = new() { Type = "string", Description = "势力间关系描述（可选），如 \"与XX宗世代同盟，与YY门为敌对关系\"" }
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
        var factionType = args.GetString("faction_type");
        var description = args.GetString("description");
        var relationshipJson = args.GetString("relationship_json");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == workId, ct);

        FactionEntity entity = null;
        if (!string.IsNullOrEmpty(id))
            entity = await db.Factions.FirstOrDefaultAsync(f => f.Id == id && f.WorkId == workId, ct);
        if (entity == null)
            entity = await db.Factions.FirstOrDefaultAsync(f => f.WorkId == workId && f.Name == name, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(factionType)) entity.FactionType = factionType;
            if (!string.IsNullOrEmpty(description)) entity.Description = description;
            if (args.Has("relationship_json")) entity.RelationshipJson = relationshipJson ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"势力「{name}」（{entity.FactionType}）已更新，ID: {entity.Id}");
        }

        var newEntity = new FactionEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Name = name,
            FactionType = factionType ?? string.Empty,
            Description = description ?? string.Empty,
            RelationshipJson = relationshipJson ?? string.Empty
        };

        await db.Factions.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"势力「{name}」（{newEntity.FactionType}）已创建，ID: {newEntity.Id}");
    }
}
