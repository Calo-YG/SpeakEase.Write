using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using System.Text.Json;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 获取当前系统时间工具：返回多种格式的时间信息
/// </summary>
public sealed class GetCurrentTimeTool : IToolExecutor
{
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_current_time",
            Description = "获取当前系统时间，返回 ISO 8601、日期、时间、时间戳等多种格式",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["timezone"] = new()
                    {
                        Type = "string",
                        Description = "时区标识，如 Asia/Shanghai、UTC，默认为本地时区"
                    }
                },
                Required = []
            }
        }
    };

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        string timezoneId = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("timezone", out var prop))
                timezoneId = prop.GetString();
        }
        catch { /* 忽略解析错误，使用默认时区 */ }

        try
        {
            var zone = !string.IsNullOrEmpty(timezoneId)
                ? TimeZoneInfo.FindSystemTimeZoneById(timezoneId)
                : TimeZoneInfo.Local;

            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime;

            var result = JsonSerializer.Serialize(new
            {
                iso8601 = now.ToString("o"),
                date = now.ToString("yyyy-MM-dd"),
                time = now.ToString("HH:mm:ss"),
                timestamp = new DateTimeOffset(now, zone.GetUtcOffset(now)).ToUnixTimeSeconds(),
                timezone = zone.Id,
                weekday = now.DayOfWeek.ToString(),
                weekday_cn = now.DayOfWeek switch
                {
                    DayOfWeek.Monday => "星期一",
                    DayOfWeek.Tuesday => "星期二",
                    DayOfWeek.Wednesday => "星期三",
                    DayOfWeek.Thursday => "星期四",
                    DayOfWeek.Friday => "星期五",
                    DayOfWeek.Saturday => "星期六",
                    DayOfWeek.Sunday => "星期日",
                    _ => now.DayOfWeek.ToString()
                }
            });

            return Task.FromResult(new ToolResult
            {
                Success = true,
                Content = result
            });
        }
        catch (TimeZoneNotFoundException)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = $"时区不存在: {timezoneId}",
                ErrorCode = "timezone_not_found"
            });
        }
    }
}
