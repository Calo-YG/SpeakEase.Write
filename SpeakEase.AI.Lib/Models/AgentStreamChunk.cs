namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// Agent 流式响应片段。
    /// </summary>
    public sealed class AgentStreamChunk
    {
        /// <summary>
        /// 片段类型：content / tool_call_delta / tool_results / iteration_end。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 文本内容增量。
        /// </summary>
        public string ContentDelta { get; set; }

        /// <summary>
        /// 工具调用增量（流式拼接用）。
        /// </summary>
        public ToolCallDelta ToolCallDelta { get; set; }

        /// <summary>
        /// 工具执行结果（Type=tool_results 时）。
        /// </summary>
        public List<ToolResult> ToolResults { get; set; }

        /// <summary>
        /// 完成的工具调用列表（Type=tool_results 时）。
        /// </summary>
        public List<ToolCall> ToolCalls { get; set; }

        /// <summary>
        /// 当前迭代轮次。
        /// </summary>
        public int Iteration { get; set; }

        /// <summary>
        /// 停止原因（Type=iteration_end 时）。
        /// </summary>
        public string StopReason { get; set; }

        /// <summary>
        /// LLM 返回的结束原因。
        /// </summary>
        public string FinishReason { get; set; }
    }
}
