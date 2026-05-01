using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class SaveWorldSettingTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SaveWorldSettingTool(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

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
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["world_rules"] = new() { Type = "string", Description = "世界规则/力量体系" },
                    ["geography"] = new() { Type = "string", Description = "地理与文明分布" },
                    ["factions"] = new() { Type = "string", Description = "势力与政治格局" },
                    ["history"] = new() { Type = "string", Description = "历史与编年事件" },
                    ["summary"] = new() { Type = "string", Description = "世界设定总摘要" }
                },
                Required = new List<string> { "work_id" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, worldRules = null, geography = null, factions = null, history = null, summary = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("world_rules", out var wr)) worldRules = wr.GetString();
            if (root.TryGetProperty("geography", out var g)) geography = g.GetString();
            if (root.TryGetProperty("factions", out var f)) factions = f.GetString();
            if (root.TryGetProperty("history", out var h)) history = h.GetString();
            if (root.TryGetProperty("summary", out var s)) summary = s.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");

        var entity = await db.WorldSettings.FirstOrDefaultAsync(x => x.WorkId == workId, ct);

        if (entity == null)
        {
            entity = new WorldSettingEntity
            {
                Id = Guid.NewGuid().ToString(),
                WorkId = workId,
            };
            db.WorldSettings.Add(entity);
        }

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

        var savedParts = string.Join(", ", jsonObj.Keys);
        return ToolResult.Ok(string.Format("世界观设定已保存，更新字段: {0}", savedParts));
    }
}
