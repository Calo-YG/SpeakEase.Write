using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetWorldSettingTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public GetWorldSettingTool(BlackboardHolder holder) => _holder = holder;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_world_setting",
            Description = "查询世界观设定，可按分区查询（world_rules/geography/factions/history），不传 section 则返回全部",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["section"] = new()
                    {
                        Type = "string",
                        Description = "分区名称：world_rules（世界规则）, geography（地理）, factions（势力）, history（历史）"
                    }
                },
                Required = new List<string>()
            }
        }
    };

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var board = _holder.Blackboard;
        if (board?.WorldSetting == null)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "当前作品暂无世界观设定",
                ErrorCode = "no_world_setting"
            });
        }

        string section = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("section", out var prop))
                section = prop.GetString();
        }
        catch
        {
        }

        var setting = board.WorldSetting;
        var result = section?.ToLowerInvariant() switch
        {
            "world_rules" => setting.WorldRules,
            "geography" => setting.Geography,
            "factions" => setting.Factions,
            "history" => setting.History,
            _ => JsonSerializer.Serialize(new
            {
                setting.WorldRules,
                setting.Geography,
                setting.Factions,
                setting.History,
                setting.LastUpdatedAt
            })
        };

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = result
        });
    }
}
