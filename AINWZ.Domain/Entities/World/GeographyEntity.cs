namespace AINWZ.Domain.Entities.World
{
    /// <summary>
    /// 地理实体，用于描述大陆、国家、城市与特殊区域。
    /// </summary>
    public class GeographyEntity : Entity, IOwner
    {
        /// <summary>
        /// 所属世界观标识。
        /// </summary>
        public string WorldSettingId { get; set; } = string.Empty;

        /// <summary>
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 地理名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 地理类型。
        /// </summary>
        public string GeographyType { get; set; } = string.Empty;

        /// <summary>
        /// 地理描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 父级地理标识。
        /// </summary>
        public string ParentGeographyId { get; set; } = string.Empty;
    }
}
