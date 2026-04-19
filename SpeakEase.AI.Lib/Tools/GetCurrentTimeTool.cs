using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 获取当前系统时间的内置工具。
/// </summary>
public static class GetCurrentTimeTool
{
    public static ToolDefinition Definition => new()
    {
        Type = "function",
        Function = new ToolFunctionDefinition
        {
            Name = "get_current_time",
            Description = "获取当前系统时间，返回 ISO 格式、本地时间、Unix 时间戳和时区信息。",
            Parameters = """
            {
                "type": "object",
                "properties": {}
            }
            """
        }
    };

    public static Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        var payload = JsonSerializer.Serialize(new
        {
            iso = now.ToString("O"),
            localTime = now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            unixTimeSeconds = now.ToUnixTimeSeconds(),
            timeZone = TimeZoneInfo.Local.Id
        });

        return Task.FromResult(new ToolResult
        {
            ToolName = "get_current_time",
            Success = true,
            Content = payload
        });
    }

    /// <summary>
    /// 注册到 Agent。
    /// </summary>
    public static void RegisterTo(ToolCapableBase agent)
    {
        agent.RegisterTool(Definition, ExecuteAsync);
    }
}
