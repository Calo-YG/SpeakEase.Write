namespace AINWZ.Domain.Entities.Users
{
    /// <summary>
    /// 用户 AI 模型配置实体，用于保存主模型、降级模型与上下文策略偏好。
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
        /// 模型分组，例如 default、fast、deep。
        /// </summary>
        public string ModelGroup { get; set; } = "default";

        /// <summary>
        /// 首选模型标识。
        /// </summary>
        public string PrimaryModelId { get; set; } = string.Empty;

        /// <summary>
        /// 备用模型标识。
        /// </summary>
        public string FallbackModelId { get; set; } = string.Empty;

        /// <summary>
        /// 上下文来源策略，例如 default-memory、global-memory、character-memory。
        /// </summary>
        public string ContextSource { get; set; } = "default-memory";

        /// <summary>
        /// 模型权重配置。
        /// </summary>
        public Dictionary<string, decimal> ModelWeights { get; set; } = new();

        /// <summary>
        /// 模型配置偏好，例如速度优先、质量优先。
        /// </summary>
        public string Preference { get; set; } = string.Empty;

        /// <summary>
        /// 配置版本号。
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// 扩展元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// 是否允许自动降级。
        /// </summary>
        public bool UseFallback { get; set; } = true;

        /// <summary>
        /// 最近同步时间。
        /// </summary>
        public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
    }
}
