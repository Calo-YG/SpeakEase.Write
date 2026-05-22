using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var entity = await db.WorldSettings.FirstOrDefaultAsync(x => x.WorkId == args.WorkId, ct);

        if (entity == null)
        {
            entity = new WorldSettingEntity
            {
                Id = idGen.NextIdString(),
                WorkId = args.WorkId,
            };
            db.WorldSettings.Add(entity);
        }

        if (!string.IsNullOrEmpty(args.WorldName)) entity.WorldName = args.WorldName;
        if (!string.IsNullOrEmpty(args.EraBackground)) entity.EraBackground = args.EraBackground;
        if (!string.IsNullOrEmpty(args.OverallStyle)) entity.OverallStyle = args.OverallStyle;
        if (!string.IsNullOrEmpty(args.Summary)) entity.Summary = args.Summary;

        var jsonObj = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(entity.JsonContent))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.JsonContent);
                if (existing != null)
                    foreach (var kv in existing) jsonObj[kv.Key] = kv.Value;
            }
            catch (JsonException)
            {
                return ToolResult.Fail("已有世界设定 JSON 格式损坏，请先手动修复后再保存", "json_parse_error");
            }
        }

        if (!string.IsNullOrEmpty(args.WorldRules)) jsonObj["worldRules"] = args.WorldRules;
        if (!string.IsNullOrEmpty(args.Geography)) jsonObj["geography"] = args.Geography;
        if (!string.IsNullOrEmpty(args.Factions)) jsonObj["factions"] = args.Factions;
        if (!string.IsNullOrEmpty(args.History)) jsonObj["history"] = args.History;

        entity.JsonContent = JsonSerializer.Serialize(jsonObj);
        await db.SaveChangesAsync(ct);

        var savedParts = new List<string>();
        if (!string.IsNullOrEmpty(args.WorldName)) savedParts.Add("worldName");
        if (!string.IsNullOrEmpty(args.EraBackground)) savedParts.Add("eraBackground");
        if (!string.IsNullOrEmpty(args.OverallStyle)) savedParts.Add("overallStyle");
        if (!string.IsNullOrEmpty(args.WorldRules)) savedParts.Add("worldRules");
        if (!string.IsNullOrEmpty(args.Geography)) savedParts.Add("geography");
        if (!string.IsNullOrEmpty(args.Factions)) savedParts.Add("factions");
        if (!string.IsNullOrEmpty(args.History)) savedParts.Add("history");

        return ToolResult.Ok(string.Format("世界观设定已保存，更新字段: {0}", string.Join(", ", savedParts)));
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string WorldName { get; init; }
        public string EraBackground { get; init; }
        public string OverallStyle { get; init; }
        public string WorldRules { get; init; }
        public string Geography { get; init; }
        public string Factions { get; init; }
        public string History { get; init; }
        public string Summary { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            return null;
        }
    }
}
