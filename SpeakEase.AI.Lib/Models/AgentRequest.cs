namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// Agent 对话请求。
    /// </summary>
    public sealed class AgentRequest
    {
        /// <summary>
        /// 指定使用的模型标识。为空时由 LLM 后端决定。
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 系统提示词。
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// 对话消息列表。
        /// </summary>
        public List<AgentMessage> Messages { get; set; } = new();

        /// <summary>
        /// 生成温度。
        /// </summary>
        public decimal? Temperature { get; set; }

        /// <summary>
        /// 最大生成 Token 数。
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// 指定使用的技能名称。为空时由 Agent 自行决定。
        /// </summary>
        public string SkillName { get; set; }

        /// <summary>
        /// Agent Loop 最大迭代次数。默认由具体 Agent 实现决定。
        /// </summary>
        public int? MaxIterations { get; set; }

        /// <summary>
        /// 请求级别的工具定义列表（由消费方或 Agent PrepareRequest 注入）。
        /// </summary>
        public List<ToolDefinition> Tools { get; set; } = new();

        /// <summary>
        /// 是否启用 Agent Loop 中的工具自动调度。默认 true。
        /// 设为 false 时，LLM 返回工具调用后不自动执行，直接返回响应。
        /// </summary>
        public bool EnableToolDispatch { get; set; } = true;
    }
}
