namespace AINWZ.Domain.Entities.Learning
{
    /// <summary>
    /// 灵感记录实体，用于保存用户或 AI 生成的剧情、角色与场景灵感。
    /// </summary>
    public class InspirationRecordEntity : Entity, IOwner
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
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 灵感类型。
        /// </summary>
        public string InspirationType { get; set; } = string.Empty;

        /// <summary>
        /// 灵感标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 灵感内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 灵感来源。
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 是否已归档。
        /// </summary>
        public bool IsArchived { get; set; }
    }
}
