namespace AINWZ.Domain.Entities.AI
{
    /// <summary>
    /// AI 生成任务实体，用于记录一次续写、润色或设定生成请求。
    /// </summary>
    public class AIGenerationTaskEntity : AggregateRootEntity, IOwner
    {
        /// <summary>
        /// 发起任务的用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId => UserId;

        /// <summary>
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所属章节标识。
        /// </summary>
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>
        /// 任务类型，例如续写、润色、生成设定。
        /// </summary>
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// 发送给模型的提示词内容。
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// 关联的上下文快照标识。
        /// </summary>
        public string ContextSnapshotId { get; set; } = string.Empty;

        /// <summary>
        /// 首选模型标识。
        /// </summary>
        public string PrimaryModelId { get; set; } = string.Empty;

        /// <summary>
        /// 备用模型标识。
        /// </summary>
        public string FallbackModelId { get; set; } = string.Empty;

        /// <summary>
        /// 当前任务状态。
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// 任务参数 JSON。
        /// </summary>
        public string ParameterJson { get; set; } = string.Empty;

        /// <summary>
        /// 任务最终输出摘要 JSON（异步任务完成后写入）。
        /// </summary>
        public string ResultJson { get; set; } = string.Empty;
    }
}
