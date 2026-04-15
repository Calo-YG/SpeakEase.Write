using System.Text.Json;
using AINWZ.Application.LLM;
using AINWZ.Infrastructure.LLM.LLM.Contract;

namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// 返回当前系统时间的内置工具。
/// </summary>
public sealed class GetCurrentTimeToolHandler : ILLMToolHandler
{
    /// <inheritdoc />
    public string Name => "get_current_time";

    /// <inheritdoc />
    public Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        var payload = JsonSerializer.Serialize(new
        {
            iso = now.ToString("O"),
            localTime = now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            unixTimeSeconds = now.ToUnixTimeSeconds(),
            timeZone = TimeZoneInfo.Local.Id
        });

        return Task.FromResult(new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            Content = payload
        });
    }
}
