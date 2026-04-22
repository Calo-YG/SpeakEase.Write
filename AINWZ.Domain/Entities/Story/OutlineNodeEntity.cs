using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story
{
    /// <summary>
    /// 大纲节点实体，表示章节级或阶段级剧情节点。
    /// </summary>
    public class OutlineNodeEntity : Entity, IOwner
    {
        /// <summary>
        /// 所属大纲标识。
        /// </summary>
        public string OutlineId { get; set; } = string.Empty;

        /// <summary>
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 父节点标识。
        /// </summary>
        public string ParentNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 节点标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 节点目标。
        /// </summary>
        public string Goal { get; set; } = string.Empty;

        /// <summary>
        /// 关键事件。
        /// </summary>
        public string KeyEvent { get; set; } = string.Empty;

        /// <summary>
        /// 节点顺序。
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// 阶段类型。
        /// </summary>
        public string StageType { get; set; } = string.Empty;

        /// <summary>
        /// 关联角色标识集合。
        /// </summary>
        public List<string> CharacterIds { get; set; } = new();
    }
}
