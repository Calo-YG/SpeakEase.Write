using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Learning
{
    /// <summary>
    /// 参考作品实体，用于管理相似作品库。
    /// </summary>
    public class ReferenceWorkEntity : AggregateRootEntity
    {
        /// <summary>
        /// 参考作品标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 作者名称。
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// 题材类型。
        /// </summary>
        public string Genre { get; set; } = string.Empty;

        /// <summary>
        /// 风格标签集合。
        /// </summary>
        public List<string> StyleTags { get; set; } = new();

        /// <summary>
        /// 综合评分。
        /// </summary>
        public decimal Score { get; set; }

        /// <summary>
        /// 摘要说明。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 数据来源。
        /// </summary>
        public string Source { get; set; } = string.Empty;
    }
}
