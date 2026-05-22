using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// 流式单轮交互增量片段。
/// LLM 策略（如 OpenAICompatible）通过此类型将每次 SSE 增量数据转发给 ReActAgent。
/// </summary>
public sealed class LLMTurnChunk
{
    /// <summary>
    /// 片段类型：content | tool_call | done
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// 内容增量（Type=content 时）
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// 工具调用增量（Type=tool_call 时）
    /// </summary>
    public ToolCallDelta ToolCallDelta { get; set; }

    /// <summary>
    /// 仅当 Type=done 时有值，包含本轮完整结果
    /// </summary>
    public LLMTurnResult TurnResult { get; set; }
}
