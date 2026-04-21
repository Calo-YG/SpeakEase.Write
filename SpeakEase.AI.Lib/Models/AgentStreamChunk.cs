namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// Agent 流式执行的增量片段
/// </summary>
public sealed class AgentStreamChunk
{
    /// <summary>
    /// 片段类型：content | tool_call | tool_result | done
    /// </summary>
    public string Type { get; set; }
    public string Content { get; set; }
    public ToolCallDelta ToolCallDelta { get; set; }
    public ToolResult ToolResult { get; set; }
    public AgentResponse FinalResponse { get; set; }
}
