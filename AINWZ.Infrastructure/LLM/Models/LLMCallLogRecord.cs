namespace AINWZ.Infrastructure.LLM.Models;

/// <summary>
/// 待持久化的 LLM 调用日志记录。
/// </summary>
public sealed class LLMCallLogRecord
{
    public string CallType { get; set; } = string.Empty;

    public string SkillName { get; set; }

    public string RequestSummary { get; set; } = string.Empty;

    public string ResponseSummary { get; set; }

    public string PrimaryModel { get; set; }

    public string FinalModel { get; set; }

    public bool UsedFallback { get; set; }

    public string FallbackModel { get; set; }

    public string RequestId { get; set; }

    public string FinishReason { get; set; }

    public string ToolCallsSummary { get; set; }

    public string ToolResultsSummary { get; set; }

    public bool Success { get; set; }

    public string ErrorMessage { get; set; }

    /// <summary>
    /// Agent Loop 停止原因：completed / max_iterations / error / empty_final_response。
    /// </summary>
    public string StopReason { get; set; }

    /// <summary>
    /// Agent Loop 总迭代次数。
    /// </summary>
    public int Iterations { get; set; }
}
