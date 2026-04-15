namespace AINWZ.Domain.Entities.Story
{
    /// <summary>
    /// 人物关系图谱边实体，用于表示图谱中的人物关系连线。
    /// </summary>
    public class CharacterGraphEdgeEntity : Entity, IOwner
    {
        /// <summary>
        /// 所属图谱标识。
        /// </summary>
        public string GraphId { get; set; } = string.Empty;

        /// <summary>
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 源节点标识。
        /// </summary>
        public string SourceNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 目标节点标识。
        /// </summary>
        public string TargetNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 关联角色关系标识。
        /// </summary>
        public string RelationshipId { get; set; } = string.Empty;

        /// <summary>
        /// 关系类型。
        /// </summary>
        public string RelationType { get; set; } = string.Empty;

        /// <summary>
        /// 展示标签。
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// 关系权重。
        /// </summary>
        public int Weight { get; set; }

        /// <summary>
        /// 方向类型。
        /// </summary>
        public string Direction { get; set; } = "directed";

        /// <summary>
        /// 边样式 JSON。
        /// </summary>
        public string StyleJson { get; set; } = string.Empty;

        /// <summary>
        /// 扩展元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
