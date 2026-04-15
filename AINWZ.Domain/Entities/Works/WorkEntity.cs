namespace AINWZ.Domain.Entities.Works
{
    /// <summary>
    /// 作品实体，表示一部小说项目的聚合根。
    /// </summary>
    public class WorkEntity : AggregateRootEntity, IOwner
    {
        /// <summary>
        /// 作者用户标识。
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 所有者标识。
        /// </summary>
        public string OwnerId => UserId;

        /// <summary>
        /// 作品名称。
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 作品简介。
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 题材类型。
        /// </summary>
        public string Genre { get; set; } = string.Empty;

        /// <summary>
        /// 风格标签。
        /// </summary>
        public List<string> StyleTags { get; set; } = new();

        /// <summary>
        /// 当前创作模式，例如 ai-led、collaborative、assist。
        /// </summary>
        public string CreationMode { get; set; } = "assist";

        /// <summary>
        /// 作品状态。
        /// </summary>
        public string Status { get; set; } = "draft";

        /// <summary>
        /// 作品总字数。
        /// </summary>
        public int TotalWordCount { get; set; }
    }
}
