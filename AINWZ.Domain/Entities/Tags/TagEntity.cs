using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Tags
{
    /// <summary>
    /// 标签实体，用于分类管理场景标签和内容标签。
    /// </summary>
    public class TagEntity : AggregateRootEntity
    {
        /// <summary>
        /// 标签名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 标签分类，scene=场景标签，content=内容标签。
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 标签颜色（十六进制或颜色名）。
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// 标签描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 使用次数。
        /// </summary>
        public int UsageCount { get; set; }
    }
}
