namespace AINWZ.Domain.Entities.AI
{
    /// <summary>
    /// AI 模型定义实体，描述系统支持的模型能力、成本与延迟画像。
    /// </summary>
    public class AIModelDefinitionEntity : AggregateRootEntity
    {
        /// <summary>
        /// 模型展示名称。
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// 模型提供方。
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// 模型说明。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 预估调用成本。
        /// </summary>
        public decimal EstimateCost { get; set; }

        /// <summary>
        /// 上下文窗口大小。
        /// </summary>
        public int ContextWindow { get; set; }

        /// <summary>
        /// 最大输出 token 数。
        /// </summary>
        public int MaxOutputTokens { get; set; }

        /// <summary>
        /// 目标延迟，单位毫秒。
        /// </summary>
        public int LatencyTargetMs { get; set; }

        /// <summary>
        /// 是否支持流式输出。
        /// </summary>
        public bool SupportsStreaming { get; set; }

        /// <summary>
        /// 是否支持工具调用。
        /// </summary>
        public bool SupportsToolCall { get; set; }

        /// <summary>
        /// 能力标签集合。
        /// </summary>
        public List<string> CapabilityTags { get; set; } = new();
    }
}
