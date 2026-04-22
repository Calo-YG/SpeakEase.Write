using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Memory
{
    /// <summary>
    /// 记忆快照实体，用于保存一次上下文组装前后的完整快照信息。
    /// </summary>
    public class MemorySnapshotEntity : AggregateRootEntity, IOwner
    {
        /// <summary>
        /// 所属用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId => UserId;

        /// <summary>
        /// 所属作品标识。
        /// </summary>
        public string WorkId { get; set; } = string.Empty;

        /// <summary>
        /// 所属章节标识。
        /// </summary>
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>
        /// 快照类型。
        /// </summary>
        public string SnapshotType { get; set; } = string.Empty;

        /// <summary>
        /// 文件系统中的快照路径。
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 快照摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 快照 JSON 内容。
        /// </summary>
        public string SnapshotJson { get; set; } = string.Empty;

        /// <summary>
        /// 快照版本标识。
        /// </summary>
        public string VersionId { get; set; } = string.Empty;
    }
}
