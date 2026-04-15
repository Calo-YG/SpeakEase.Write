using AINWZ.Infrastructure.LLM.Contract;

namespace AINWZ.Infrastructure.LLM.Models;

/// <summary>
/// 表示一次 LLM 流式输出中的标准事件。
/// </summary>
public sealed class LLMStreamEvent
{
    public string Type { get; set; } = string.Empty;

    public string RequestId { get; set; }

    public string Model { get; set; }

    public string FromModel { get; set; }

    public string ToModel { get; set; }

    public string Content { get; set; }

    public bool UsedFallback { get; set; }

    public string ErrorCode { get; set; }

    public string ErrorMessage { get; set; }

    public LLMToolCallDelta ToolCallDelta { get; set; }

    public List<LLMToolCall> ToolCalls { get; set; } = new();

    public string FinishReason { get; set; }

    public List<LLMToolExecutionResult> ToolResults { get; set; } = new();
}
