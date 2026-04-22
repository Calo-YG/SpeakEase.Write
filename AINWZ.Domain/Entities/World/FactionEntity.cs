using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.World
{
    /// <summary>
    /// 势力实体，用于描述门派、家族、国家或组织。
    /// </summary>
    public class FactionEntity : Entity, IOwner
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
        /// 势力名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 势力类型。
        /// </summary>
        public string FactionType { get; set; } = string.Empty;

        /// <summary>
        /// 势力描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 势力关系 JSON。
        /// </summary>
        public string RelationshipJson { get; set; } = string.Empty;
    }
}
