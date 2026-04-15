namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// 工具执行结果。
/// </summary>
public sealed class LLMToolExecutionResult
{
    public string ToolCallId { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string Content { get; set; } = string.Empty;

    public string ErrorCode { get; set; }
}
