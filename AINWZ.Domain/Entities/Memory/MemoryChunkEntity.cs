namespace AINWZ.Domain.Entities.Memory
{
    /// <summary>
    /// 记忆片段实体，表示可用于上下文组装的文件系统记忆块。
    /// </summary>
    public class MemoryChunkEntity : Entity, IOwner
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
        /// 片段顺序。
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// 记忆类型，例如 context、summary、character。
        /// </summary>
        public string MemoryType { get; set; } = "context";

        /// <summary>
        /// 记忆正文。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 片段来源。
        /// </summary>
        public string Source { get; set; } = "editor";

        /// <summary>
        /// 版本标识。
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// 生成模型标识。
        /// </summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// 是否固定保留。
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// 扩展元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
