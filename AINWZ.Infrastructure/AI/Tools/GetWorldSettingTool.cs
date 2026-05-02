using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetWorldSettingTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GetWorldSettingTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Function = new FunctionDefinition
        {
            Name = "get_world_setting",
            Description = "查询世界观设定。可按分区查询（world_rules/geography/factions/history）。",
            Parameters = new FunctionParameters
            {
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID" },
                    ["section"] = new()
                    {
                        Type = "string",
                        Description = "分区名称（可选）：world_rules=世界规则、geography=地理、factions=势力、history=历史",
                        Enum = new List<object> { "world_rules", "geography", "factions", "history" }
                    }
                },
                Required = new List<string> { "work_id" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        string workId = null;
        string section = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (doc.RootElement.TryGetProperty("section", out var s)) section = s.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId))
            return new ToolResult { Success = false, Content = "缺少 work_id 参数", ErrorCode = "missing_parameter" };

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var ws = await db.WorldSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId, ct);
        if (ws == null)
            return new ToolResult { Content = "未找到世界观设定" };

        WorldRules parsed = null;
        if (!string.IsNullOrEmpty(ws.JsonContent))
        {
            try { parsed = JsonSerializer.Deserialize<WorldRules>(ws.JsonContent); }
            catch { }
        }

        var worldRules = parsed?.WorldRulesText ?? ws.Summary ?? string.Empty;
        var geography = parsed?.Geography ?? string.Empty;
        var factions = parsed?.Factions ?? string.Empty;
        var history = parsed?.History ?? string.Empty;

        if (!string.IsNullOrEmpty(section))
        {
            return section switch
            {
                "world_rules" => new ToolResult { Success = true, Content = worldRules },
                "geography" => new ToolResult { Success = true, Content = geography },
                "factions" => new ToolResult { Success = true, Content = factions },
                "history" => new ToolResult { Success = true, Content = history },
                _ => new ToolResult { Success = false, Content = $"未知分区: {section}，支持: world_rules/geography/factions/history" }
            };
        }

        var sb = new StringBuilder();
        sb.AppendLine(worldRules);
        if (!string.IsNullOrEmpty(geography)) sb.AppendLine($"地理：{geography}");
        if (!string.IsNullOrEmpty(factions)) sb.AppendLine($"势力：{factions}");
        if (!string.IsNullOrEmpty(history)) sb.AppendLine($"历史：{history}");

        return new ToolResult
        {
            Success = true,
            Content = sb.ToString()
        };
    }

    private class WorldRules
    {
        [System.Text.Json.Serialization.JsonPropertyName("worldRules")]
        public string WorldRulesText { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("geography")]
        public string Geography { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("factions")]
        public string Factions { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("history")]
        public string History { get; set; }
    }
}
