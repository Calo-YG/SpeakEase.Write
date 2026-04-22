using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Domain.Entities.Story
{
    /// <summary>
    /// 伏笔实体，用于追踪埋设、触发与回收状态。
    /// </summary>
    public class ForeshadowingEntity : Entity, IOwner
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
        /// 伏笔标题。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 伏笔描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 埋设章节标识。
        /// </summary>
        public string SetupChapterId { get; set; } = string.Empty;

        /// <summary>
        /// 回收章节标识。
        /// </summary>
        public string PayoffChapterId { get; set; } = string.Empty;

        /// <summary>
        /// 伏笔状态。
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// 重要程度。
        /// </summary>
        public int Importance { get; set; }
    }
}
