using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class SaveWorldSettingTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "save_world_setting",
            Description = "保存或更新世界观设定，传入一个或多个分区的文本内容",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["world_name"] = new() { Type = "string", Description = "世界名称（可选），如: 九洲大陆、星辰界" },
                    ["era_background"] = new() { Type = "string", Description = "时代背景（可选），如: 末法时代、诸国争霸" },
                    ["overall_style"] = new() { Type = "string", Description = "整体风格（可选），如: 东方玄幻、西方奇幻" },
                    ["world_rules"] = new() { Type = "string", Description = "世界规则/力量体系（可选）" },
                    ["geography"] = new() { Type = "string", Description = "地理与文明分布（可选）" },
                    ["factions"] = new() { Type = "string", Description = "势力与政治格局（可选）" },
                    ["history"] = new() { Type = "string", Description = "历史与编年事件（可选）" },
                    ["summary"] = new() { Type = "string", Description = "世界设定总摘要（可选）" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var worldName = args.GetString("world_name");
        var eraBackground = args.GetString("era_background");
        var overallStyle = args.GetString("overall_style");
        var worldRules = args.GetString("world_rules");
        var geography = args.GetString("geography");
        var factions = args.GetString("factions");
        var history = args.GetString("history");
        var summary = args.GetString("summary");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var entity = await db.WorldSettings.FirstOrDefaultAsync(x => x.WorkId == workId, ct);

        if (entity == null)
        {
            entity = new WorldSettingEntity
            {
                Id = idGen.NextIdString(),
                WorkId = workId,
            };
            db.WorldSettings.Add(entity);
        }

        if (!string.IsNullOrEmpty(worldName)) entity.WorldName = worldName;
        if (!string.IsNullOrEmpty(eraBackground)) entity.EraBackground = eraBackground;
        if (!string.IsNullOrEmpty(overallStyle)) entity.OverallStyle = overallStyle;
        if (!string.IsNullOrEmpty(summary)) entity.Summary = summary;

        var jsonObj = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(entity.JsonContent))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.JsonContent);
                if (existing != null)
                    foreach (var kv in existing) jsonObj[kv.Key] = kv.Value;
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(worldRules)) jsonObj["worldRules"] = worldRules;
        if (!string.IsNullOrEmpty(geography)) jsonObj["geography"] = geography;
        if (!string.IsNullOrEmpty(factions)) jsonObj["factions"] = factions;
        if (!string.IsNullOrEmpty(history)) jsonObj["history"] = history;

        entity.JsonContent = JsonSerializer.Serialize(jsonObj);
        await db.SaveChangesAsync(ct);

        var savedParts = new List<string>();
        if (!string.IsNullOrEmpty(worldName)) savedParts.Add("worldName");
        if (!string.IsNullOrEmpty(eraBackground)) savedParts.Add("eraBackground");
        if (!string.IsNullOrEmpty(overallStyle)) savedParts.Add("overallStyle");
        savedParts.AddRange(jsonObj.Keys);

        return ToolResult.Ok(string.Format("世界观设定已保存，更新字段: {0}", string.Join(", ", savedParts)));
    }
}
