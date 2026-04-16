namespace AINWZ.Domain.Entities.AI
{
    /// <summary>
    /// 章节分析结果实体，记录 AI 对章节内容的自动分析输出（伏笔/角色变化等），
    /// 支持用户逐条确认或忽略。
    /// </summary>
    public class ChapterAnalysisResultEntity : Entity, IOwner
    {
        /// <summary>
        /// 关联的 AI 生成任务标识。
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
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所属章节标识。
        /// </summary>
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>
        /// 分析类型，例如 foreshadowing、character-arc、relationship。
        /// </summary>
        public string AnalysisType { get; set; } = string.Empty;

        /// <summary>
        /// LLM 原始 JSON 输出。
        /// </summary>
        public string ResultJson { get; set; } = string.Empty;

        /// <summary>
        /// 分析结果摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 基于此分析自动创建的实体标识列表。
        /// </summary>
        public List<string> CreatedEntityIds { get; set; } = new();

        /// <summary>
        /// 用户是否已确认采纳。
        /// </summary>
        public bool IsConfirmed { get; set; }

        /// <summary>
        /// 用户反馈，例如 accepted、ignored、modified。
        /// </summary>
        public string UserFeedback { get; set; } = string.Empty;
    }
}
