using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using System.Text.Json.Serialization;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 世界观设定查询工具：查询世界观设定，支持按分区 world_rules/geography/factions/history 筛选
public sealed class GetWorldSettingTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_world_setting",
            Description = "查询世界观设定。可按分区查询（world_rules/geography/factions/history）。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["section"] = new()
                    {
                        Type = "string",
                        Description = "分区名称（可选），枚举值: world_rules=世界规则、geography=地理、factions=势力、history=历史",
                        Enum = new List<object> { "world_rules", "geography", "factions", "history" }
                    }
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

        var ws = await db.WorldSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == args.WorkId, ct);
        if (ws == null)
            return ToolResult.Fail("未找到世界观设定", "not_found");

        WorldRules parsed = null;
        if (!string.IsNullOrEmpty(ws.JsonContent))
        {
            try { parsed = JsonSerializer.Deserialize<WorldRules>(ws.JsonContent); }
            catch (JsonException) { }
        }

        var worldRules = parsed?.WorldRulesText ?? ws.Summary ?? string.Empty;
        var geography = parsed?.Geography ?? string.Empty;
        var factions = parsed?.Factions ?? string.Empty;
        var history = parsed?.History ?? string.Empty;

        if (!string.IsNullOrEmpty(args.Section))
        {
            return args.Section switch
            {
                "world_rules" => ToolResult.Ok(worldRules),
                "geography" => ToolResult.Ok(geography),
                "factions" => ToolResult.Ok(factions),
                "history" => ToolResult.Ok(history),
                _ => ToolResult.Fail($"未知分区: {args.Section}，支持: world_rules/geography/factions/history", "invalid_section")
            };
        }

        var sb = new StringBuilder();
        sb.AppendLine(worldRules);
        if (!string.IsNullOrEmpty(geography)) sb.AppendLine($"地理：{geography}");
        if (!string.IsNullOrEmpty(factions)) sb.AppendLine($"势力：{factions}");
        if (!string.IsNullOrEmpty(history)) sb.AppendLine($"历史：{history}");

        return ToolResult.Ok(sb.ToString());
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Section { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            return null;
        }
    }

    private class WorldRules
    {
        [JsonPropertyName("worldRules")]
        public string WorldRulesText { get; set; }
        [JsonPropertyName("geography")]
        public string Geography { get; set; }
        [JsonPropertyName("factions")]
        public string Factions { get; set; }
        [JsonPropertyName("history")]
        public string History { get; set; }
    }
}
