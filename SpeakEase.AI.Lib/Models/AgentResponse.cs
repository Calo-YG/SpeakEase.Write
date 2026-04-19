namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// Agent 对话响应。
    /// </summary>
    public sealed class AgentResponse
    {
        /// <summary>
        /// 使用的模型标识。
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 响应文本内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// LLM 返回的工具调用列表。
        /// </summary>
        public List<ToolCall> ToolCalls { get; set; } = new();

        /// <summary>
        /// 工具执行结果列表。
        /// </summary>
        public List<ToolResult> ToolResults { get; set; } = new();

        /// <summary>
        /// 停止原因：completed / max_iterations / error / tool_calls。
        /// </summary>
        public string StopReason { get; set; }

        /// <summary>
        /// Agent Loop 总迭代次数。
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// 完整对话历史（含所有迭代中的 assistant + tool 消息）。
        /// </summary>
        public List<AgentMessage> ConversationHistory { get; set; } = new();
    }
}
