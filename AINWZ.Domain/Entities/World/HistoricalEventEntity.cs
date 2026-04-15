namespace AINWZ.Domain.Entities.World
{
    /// <summary>
    /// 历史事件实体，用于记录世界观中的重大历史背景。
    /// </summary>
    public class HistoricalEventEntity : Entity, IOwner
    {
        /// <summary>
        /// 所属世界观标识。
        /// </summary>
        public string WorldSettingId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 历史事件标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 历史事件描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 所属时代标签。
        /// </summary>
        public string EraLabel { get; set; } = string.Empty;

        /// <summary>
        /// 事件发生时间。
        /// </summary>
        public DateTime EventTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 影响摘要。
        /// </summary>
        public string ImpactSummary { get; set; } = string.Empty;
    }
}
