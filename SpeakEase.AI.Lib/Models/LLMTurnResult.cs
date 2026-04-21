using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// 单轮 LLM 交互结果（策略内部完成累积，ReActAgent 无需关心协议解析）
/// </summary>
public sealed class LLMTurnResult
{
    /// <summary>
    /// 本轮模型输出的文本内容
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// 本轮模型请求执行的工具调用列表
    /// </summary>
    public List<ToolCall> ToolCalls { get; set; }

    /// <summary>
    /// 实际使用的模型标识
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// 本轮 Token 用量
    /// </summary>
    public UsageInfo Usage { get; set; }

    /// <summary>
    /// 本轮是否包含工具调用
    /// </summary>
    public bool HasToolCalls => ToolCalls?.Count > 0;
}
