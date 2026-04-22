using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.AI
{
    /// <summary>
    /// AI 生成结果实体，用于保存多版本候选内容与反馈数据。
    /// </summary>
    public class AIGenerationResultEntity : Entity, IOwner
    {
        /// <summary>
        /// 所属任务标识。
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// 所属用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId => UserId;

        /// <summary>
        /// 生成结果对应的模型标识。
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// 结果版本号。
        /// </summary>
        public int VersionNumber { get; set; }

        /// <summary>
        /// 生成内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 结果摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 模型置信度评分。
        /// </summary>
        public decimal ConfidenceScore { get; set; }

        /// <summary>
        /// 关键词集合。
        /// </summary>
        public List<string> Keywords { get; set; } = new();

        /// <summary>
        /// 用户反馈状态。
        /// </summary>
        public string FeedbackStatus { get; set; } = string.Empty;

        /// <summary>
        /// 是否已被采纳。
        /// </summary>
        public bool IsAccepted { get; set; }
    }
}
