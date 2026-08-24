using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// Agent 流式执行的增量片段。
/// ReActAgent 将 LLMTurnChunk 转换为更上层友好的格式，通过此类型推送。
/// </summary>
public sealed class AgentStreamChunk
{
    public string RunId { get; set; }
    public string StepId { get; set; }
    public long Sequence { get; set; }
    /// <summary>
    /// 片段类型：meta | content | tool_call | tool_result | done
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// 内容类型：chapter | character | outline | setting | audit_report | plain | system
    /// 仅在 Type=meta 时有意义，前端据此选择渲染组件
    /// </summary>
    public string ContentType { get; set; }

    public string Content { get; set; }
    public ToolCallDelta ToolCallDelta { get; set; }
    public ToolCall ToolCall { get; set; }
    public ToolResult ToolResult { get; set; }
    public AgentResponse FinalResponse { get; set; }
}
