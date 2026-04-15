using System.Collections.Generic;

namespace AINWZ
{
    /// <summary>
    /// 记忆片段请求对象。
    /// </summary>
    public class MemoryChunkRequest
    {
        /// <summary>
        /// 用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 作品标识。
        /// </summary>
        public string ProductId { get; set; } = string.Empty;

        /// <summary>
        /// 章节标识。
        /// </summary>
        public string ChapterId { get; set; } = string.Empty;

        /// <summary>
        /// 片段顺序。
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// 记忆类型。
        /// </summary>
        public string MemoryType { get; set; } = null;

        /// <summary>
        /// 记忆正文。
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 扩展元数据。
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// 片段来源。
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 关联模型标识。
        /// </summary>
        public string ModelId { get; set; }

        /// <summary>
        /// 是否固定保留。
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// 指定版本标识。
        /// </summary>
        public string VersionId { get; set; } = null;
    }
}
