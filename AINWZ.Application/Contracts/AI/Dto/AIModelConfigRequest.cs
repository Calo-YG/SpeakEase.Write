namespace AINWZ.Application.Contracts.AI.Dto
{
    /// <summary>
    /// AI 模型配置请求对象。
    /// </summary>
    public class AIModelConfigRequest
    {
        /// <summary>
        /// 用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 模型分组。
        /// </summary>
        public string ModelGroup { get; set; } = null;

        /// <summary>
        /// 首选模型标识。
        /// </summary>
        public string PrimaryModelId { get; set; }

        /// <summary>
        /// 降级模型标识。
        /// </summary>
        public string FallbackModelId { get; set; }

        /// <summary>
        /// 模型权重配置。
        /// </summary>
        public Dictionary<string, decimal> ModelWeights { get; set; } = null;

        /// <summary>
        /// 用户偏好。
        /// </summary>
        public string Preference { get; set; }

        /// <summary>
        /// 扩展元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; }
    }
}
