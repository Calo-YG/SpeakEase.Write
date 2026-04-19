namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// 子 Agent 执行结果。
    /// 主 Agent 只接收此摘要，子 Agent 的完整对话历史被丢弃。
    /// </summary>
    public sealed class SubAgentResult
    {
        /// <summary>
        /// 子 Agent 是否成功完成任务。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 子 Agent 的最终输出内容（摘要）。
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 子 Agent 执行过程中的工具调用结果摘要。
        /// </summary>
        public List<SubAgentToolSummary> ToolSummaries { get; set; } = new();

        /// <summary>
        /// 子 Agent 的迭代次数。
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// 子 Agent 的停止原因。
        /// </summary>
        public string StopReason { get; set; }

        /// <summary>
        /// 错误信息（执行失败时）。
        /// </summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// 子 Agent 工具调用摘要。
    /// 只保留关键信息，不保留完整参数和输出。
    /// </summary>
    public sealed class SubAgentToolSummary
    {
        /// <summary>
        /// 工具名称。
        /// </summary>
        public string ToolName { get; set; }

        /// <summary>
        /// 是否执行成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 工具输出摘要（可能被截断）。
        /// </summary>
        public string ResultSummary { get; set; }
    }
}
