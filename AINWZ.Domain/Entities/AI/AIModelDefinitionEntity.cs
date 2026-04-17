namespace AINWZ.Domain.Entities.AI
{
    /// <summary>
    /// 模型提供商实体，由管理员维护，管理提供商及其下可用模型列表。
    /// 纯系统级数据，用户只能选择使用。
    /// </summary>
    public class AIModelDefinitionEntity : AggregateRootEntity
    {
        /// <summary>
        /// 提供商展示名称，例如 "OpenAI"、"Anthropic"。
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// 提供商标识，例如 "openai"、"anthropic"、"deepseek"。
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// 提供商说明。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// API 基础地址，例如 "https://api.openai.com/v1"。
        /// </summary>
        public string ApiBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// API 密钥。
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用。
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
