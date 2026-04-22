using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Works
{
    /// <summary>
    /// 卷实体，用于组织作品章节结构。
    /// </summary>
    public class VolumeEntity : Entity, IOwner
    {
        /// <summary>
        /// 作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 卷名称。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 卷序号。
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// 卷摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;
    }
}
