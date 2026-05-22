namespace SpeakEase.AI.Lib.Models;

using SpeakEase.AI.Lib.OpenAIModel;

/// <summary>
/// Agent 执行响应：包含最终回复内容、Token 用量、迭代次数、停止原因等。
/// </summary>
public sealed class AgentResponse
{
    /// <summary>
    /// Agent 最终回复的文本内容
    /// </summary>
    public string Content { get; set; }
    /// <summary>
    /// 思维链内容（DeepSeek R1 等模型的思考过程）
    /// </summary>
    public string ReasoningContent { get; set; }
    /// <summary>
    /// 实际使用的模型名称
    /// </summary>
    public string Model { get; set; }
    /// <summary>
    /// 所有已执行的工具调用结果列表
    /// </summary>
    public List<ToolResult> ToolResults { get; set; } = new();
    /// <summary>
    /// 完整对话历史（含 System/User/Assistant/Tool 消息），可用于后续对话
    /// </summary>
    public List<ChatMessage> ConversationHistory { get; set; } = new();
    /// <summary>
    /// 实际执行轮次
    /// </summary>
    public int Iterations { get; set; }
    /// <summary>
    /// 停止原因：completed / llm_error / max_iterations_reached
    /// </summary>
    public string StopReason { get; set; }
    /// <summary>
    /// 全流程累计 Token 用量
    /// </summary>
    public UsageInfo TotalUsage { get; set; }
}
