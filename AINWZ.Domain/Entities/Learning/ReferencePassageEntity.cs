using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Learning
{
    /// <summary>
    /// 高分段落实体，用于沉淀参考片段及技巧分析。
    /// </summary>
    public class ReferencePassageEntity : Entity
    {
        /// <summary>
        /// 所属参考作品标识。
        /// </summary>
        public string ReferenceWorkId { get; set; } = string.Empty;

        /// <summary>
        /// 段落类型。
        /// </summary>
        public string PassageType { get; set; } = string.Empty;

        /// <summary>
        /// 段落正文。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 高亮标签 JSON。
        /// </summary>
        public string HighlightTagsJson { get; set; } = string.Empty;

        /// <summary>
        /// 技巧分析内容。
        /// </summary>
        public string TechniqueAnalysis { get; set; } = string.Empty;

        /// <summary>
        /// 收藏次数。
        /// </summary>
        public int FavoriteCount { get; set; }

        /// <summary>
        /// 推荐次数。
        /// </summary>
        public int RecommendationCount { get; set; }
    }
}
