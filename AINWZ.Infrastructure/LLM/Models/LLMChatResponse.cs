using AINWZ.Infrastructure.LLM.Contract;

namespace AINWZ.Infrastructure.LLM.Models;

/// <summary>
/// LLM 对话响应对象。
/// </summary>
public sealed class LLMChatResponse
{
    public string PrimaryModel { get; set; } = string.Empty;

    public string FinalModel { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public bool UsedFallback { get; set; }

    public string FallbackModel { get; set; }

    public string Content { get; set; } = string.Empty;

    public string RawResponse { get; set; } = string.Empty;

    public string RequestId { get; set; }

    public int? PromptTokens { get; set; }

    public int? CompletionTokens { get; set; }

    public int? TotalTokens { get; set; }

    public string FinishReason { get; set; }

    public List<LLMToolCall> ToolCalls { get; set; } = new();

    public List<LLMToolExecutionResult> ToolResults { get; set; } = new();

    /// <summary>
    /// Agent Loop 停止原因：completed / max_iterations / error / empty_final_response。
    /// </summary>
    public string StopReason { get; set; }

    /// <summary>
    /// Agent Loop 总迭代次数。
    /// </summary>
    public int Iterations { get; set; }

    /// <summary>
    /// 跨所有迭代的完整对话历史（含 assistant + tool 消息）。
    /// </summary>
    public List<LLMChatMessage> ConversationHistory { get; set; } = new();
}
