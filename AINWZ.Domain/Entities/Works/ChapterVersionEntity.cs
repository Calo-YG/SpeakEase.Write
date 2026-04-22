using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Works
{
    /// <summary>
    /// 章节版本实体，用于保存自动保存、AI 生成和手动修改历史。
    /// </summary>
    public class ChapterVersionEntity : Entity, IOwner
    {
        /// <summary>
        /// 章节标识。
        /// </summary>
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 版本号。
        /// </summary>
        public int VersionNumber { get; set; }

        /// <summary>
        /// 版本内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 版本摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 版本来源，例如 manual、autosave、ai-generate。
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 创建该版本所使用的模型标识。
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
    }
}
