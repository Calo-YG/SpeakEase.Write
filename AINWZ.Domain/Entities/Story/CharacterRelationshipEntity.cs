namespace AINWZ.Domain.Entities.Story
{
    /// <summary>
    /// 角色关系实体，用于描述人物关系网络。
    /// </summary>
    public class CharacterRelationshipEntity : Entity, IOwner
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
        /// 源角色标识。
        /// </summary>
        public string SourceCharacterId { get; set; } = string.Empty;

        /// <summary>
        /// 目标角色标识。
        /// </summary>
        public string TargetCharacterId { get; set; } = string.Empty;

        /// <summary>
        /// 关系类型。
        /// </summary>
        public string RelationshipType { get; set; } = string.Empty;

        /// <summary>
        /// 关系描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 关系强度。
        /// </summary>
        public int Intensity { get; set; }
    }
}
