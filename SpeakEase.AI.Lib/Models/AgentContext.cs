namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// Agent 运行时上下文，在 IChatAgentFilter 管道中传递。
    /// </summary>
    public sealed class AgentContext
    {
        /// <summary>
        /// 当前 Agent 名称。
        /// </summary>
        public string AgentName { get; init; }

        /// <summary>
        /// 当前请求。
        /// </summary>
        public AgentRequest Request { get; init; }

        /// <summary>
        /// 跨 Filter 传递的共享数据字典。
        /// </summary>
        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();

        /// <summary>
        /// 当前 Agent Loop 迭代轮次（从 1 开始）。
        /// </summary>
        public int Iteration { get; set; }

        /// <summary>
        /// 当前使用的技能名称。
        /// </summary>
        public string SkillName { get; set; }
    }
}
