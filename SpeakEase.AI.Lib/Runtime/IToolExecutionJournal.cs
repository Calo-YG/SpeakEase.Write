using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// Tool 副作用执行日志。Runtime 通过它恢复已完成调用，避免重试重复执行。
/// </summary>
public interface IToolExecutionJournal
{
    Task<ToolExecutionLease> BeginAsync(
        string runId,
        string stepId,
        ToolCall toolCall,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        string runId,
        string stepId,
        ToolCall toolCall,
        ToolResult result,
        CancellationToken cancellationToken = default);
}

public sealed class ToolExecutionLease
{
    public bool ShouldExecute { get; init; }
    public ToolResult ReplayResult { get; init; }

    public static ToolExecutionLease Execute() => new() { ShouldExecute = true };

    public static ToolExecutionLease Replay(ToolResult result) => new()
    {
        ShouldExecute = false,
        ReplayResult = result
    };
}
