using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story
{
    /// <summary>
    /// 剧情大纲实体，表示一部作品的结构化剧情规划。
    /// </summary>
    public class OutlineEntity : AggregateRootEntity, IOwner
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
        /// 大纲标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 结构模板名称。
        /// </summary>
        public string StructureTemplate { get; set; } = string.Empty;

        /// <summary>
        /// 大纲摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 大纲结构化 JSON。
        /// </summary>
        public string JsonContent { get; set; } = string.Empty;

        /// <summary>
        /// 是否为主大纲。
        /// </summary>
        public bool IsPrimary { get; set; }
    }
}
