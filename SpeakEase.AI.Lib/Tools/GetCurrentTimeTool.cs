using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 获取当前系统时间的内置工具。
/// </summary>
public sealed class GetCurrentTimeTool:IToolExecutor
{
    /// <summary>
    /// 工具定义：工具类型为 "function"，函数名称为 "get_current_time"，描述说明工具的功能，参数定义为空对象。
    /// </summary>
    private static ToolDefinition Definition => new()
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

    /// <summary>
    /// 
    /// </summary>
    public ToolDefinition ToolDefinition => Definition;

    /// <summary>
    /// 工具执行逻辑：获取当前系统时间，并返回包含 ISO 格式、本地时间、Unix 时间戳和时区信息的 JSON 字符串。
    /// </summary>
    /// <param name="arguments"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
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
}
