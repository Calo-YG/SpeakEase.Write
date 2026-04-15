namespace AINWZ.Domain.Entities.World
{
    /// <summary>
    /// 世界观实体，保存作品的底层世界设定。
    /// </summary>
    public class WorldSettingEntity : AggregateRootEntity, IOwner
    {
        /// <summary>
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 世界名称。
        /// </summary>
        public string WorldName { get; set; } = string.Empty;

        /// <summary>
        /// 时代背景。
        /// </summary>
        public string EraBackground { get; set; } = string.Empty;

        /// <summary>
        /// 整体风格。
        /// </summary>
        public string OverallStyle { get; set; } = string.Empty;

        /// <summary>
        /// 世界观摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 世界观结构化 JSON 内容。
        /// </summary>
        public string JsonContent { get; set; } = string.Empty;
    }
}
