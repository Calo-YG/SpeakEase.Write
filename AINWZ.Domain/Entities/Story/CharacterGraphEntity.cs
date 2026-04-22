using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story
{
    /// <summary>
    /// 人物关系图谱实体，用于保存某个作品下的人物关系图谱快照。
    /// </summary>
    public class CharacterGraphEntity : AggregateRootEntity, IOwner
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
        /// 图谱名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 图谱描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 图谱版本号。
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// 图谱状态。
        /// </summary>
        public string Status { get; set; } = "draft";

        /// <summary>
        /// 前端布局 JSON。
        /// </summary>
        public string LayoutJson { get; set; } = string.Empty;

        /// <summary>
        /// 扩展元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
