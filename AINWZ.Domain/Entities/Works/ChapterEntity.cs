using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Works
{
    /// <summary>
    /// 章节实体，保存正文、摘要、字数和关联剧情信息。
    /// </summary>
    public class ChapterEntity : AggregateRootEntity, IOwner
    {
        /// <summary>
        /// 作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 卷标识。
        /// </summary>
        public string VolumeId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 章节标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 章节序号。
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// 当前章节正文。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 章节摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 当前字数。
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// 章节状态。
        /// </summary>
        public string Status { get; set; } = "draft";

        /// <summary>
        /// 关联大纲节点标识列表。
        /// </summary>
        public List<string> OutlineNodeIds { get; set; } = new();

        /// <summary>
        /// 最后一次内容保存时间（UTC）。
        /// </summary>
        public DateTime? LastContentSavedAt { get; set; }

        /// <summary>
        /// 作者备注，用户自由记录本章要点/注意事项，AI续写时注入上下文。
        /// </summary>
        public string AuthorNotes { get; set; } = string.Empty;
    }
}
