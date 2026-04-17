namespace AINWZ.Domain.Entities.Users
{
    /// <summary>
    /// 用户 AI 模型配置实体，用户管理自己使用的模型提供商及模型。
    /// 支持多配置，但同一用户只能有一个激活配置。
    /// </summary>
    public class UserAiModelConfigEntity : Entity, IOwner
    {
        /// <summary>
        /// 所属用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId => UserId;

        /// <summary>
        /// 配置名称，例如 "日常续写"、"深度分析"，用户自定义。
        /// </summary>
        public string ConfigName { get; set; } = string.Empty;

        /// <summary>
        /// 首选提供商标识，指向 AIModelDefinitionEntity.Id。
        /// </summary>
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>
        /// 首选模型标识，取自提供商 ModelsJson 中的 ProviderModelItem.Id。
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// 备用提供商标识，指向 AIModelDefinitionEntity.Id。
        /// </summary>
        public string FallbackProviderId { get; set; } = string.Empty;

        /// <summary>
        /// 备用模型标识，取自备用提供商 ModelsJson 中的 ProviderModelItem.Id。
        /// </summary>
        public string FallbackModelName { get; set; } = string.Empty;

        /// <summary>
        /// 是否为当前激活配置。同一用户只能有一个激活。
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 模型配置偏好，例如 speed、quality。
        /// </summary>
        public string Preference { get; set; } = string.Empty;

        /// <summary>
        /// 是否允许自动降级到备用模型。
        /// </summary>
        public bool UseFallback { get; set; } = true;

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

        /// <summary>
        /// 最近同步时间。
        /// </summary>
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
    }
}
