namespace SpeakEase.Write.Domain
{
    /// <summary>
    /// 所有领域实体的基础抽象，统一提供标识与审计字段。
    /// </summary>
    public abstract class Entity
    {
        /// <summary>
        /// 实体唯一标识。
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 创建人标识。
        /// </summary>
        public string CreateBy { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间（UTC）。
        /// </summary>
        public DateTime CreateAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后更新人标识。
        /// </summary>
        public string UpdateBy { get; set; } = string.Empty;

        /// <summary>
        /// 最后更新时间（UTC）。
        /// </summary>
        public DateTime UpdateAt { get; set; } = DateTime.UtcNow;
    }
}
