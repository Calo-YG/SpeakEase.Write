using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story
{
    /// <summary>
    /// 人物关系图谱节点实体，用于表示图谱中的角色节点。
    /// </summary>
    public class CharacterGraphNodeEntity : Entity, IOwner
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
        /// 关联角色标识。
        /// </summary>
        public string CharacterId { get; set; } = string.Empty;

        /// <summary>
        /// 节点展示名称。
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 节点类型。
        /// </summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// 重要程度。
        /// </summary>
        public int Importance { get; set; }

        /// <summary>
        /// X 坐标。
        /// </summary>
        public decimal X { get; set; }

        /// <summary>
        /// Y 坐标。
        /// </summary>
        public decimal Y { get; set; }

        /// <summary>
        /// 节点样式 JSON。
        /// </summary>
        public string StyleJson { get; set; } = string.Empty;

        /// <summary>
        /// 扩展元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
