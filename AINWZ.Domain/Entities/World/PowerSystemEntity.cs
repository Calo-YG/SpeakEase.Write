using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.World
{
    /// <summary>
    /// 力量体系实体，用于定义修炼等级与能力结构。
    /// </summary>
    public class PowerSystemEntity : Entity, IOwner
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
        /// 力量体系名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 等级定义 JSON。
        /// </summary>
        public string LevelDefinitionJson { get; set; } = string.Empty;

        /// <summary>
        /// 能力规则描述。
        /// </summary>
        public string AbilityRule { get; set; } = string.Empty;

        /// <summary>
        /// 资源系统说明。
        /// </summary>
        public string ResourceSystem { get; set; } = string.Empty;
    }
}
