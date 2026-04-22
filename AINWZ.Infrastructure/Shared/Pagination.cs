namespace SpeakEase.Write.Infrastructure.Shared
{
    /// <summary>
    /// 分页响应
    /// </summary>
    public sealed class Pagination
    {
        /// <summary>
        /// 分页类型
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// 每页数量
        /// </summary>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// 排序字段
        /// </summary>
        public string Order { get; set; } = "CreatedAt";

        /// <summary>
        /// 排序
        /// </summary>
        public string OrderBy { get; set; } = "desc";
    }
}
