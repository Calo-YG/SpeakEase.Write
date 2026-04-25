namespace SpeakEase.Write.Infrastructure.AI.Context
{
    /// <summary>
    /// Agent 上下文
    /// </summary>
    public sealed class AgentContext
    {
        /// <summary>
        /// 历史对话
        /// </summary>
        public List<string> HistoryMessage { get; set; }

        /// <summary>
        /// 书籍本身
        /// </summary>
        public string ProjectMemory { get; set; }

        /// <summary>
        /// 单词对话请求Id
        /// </summary>
        public string RequestId { get; set; }
    }
}
