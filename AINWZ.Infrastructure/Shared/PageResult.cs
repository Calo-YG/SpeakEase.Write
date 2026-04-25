namespace SpeakEase.Write.Infrastructure.Shared
{
    public class PageResult<T>
    {
        /// <summary>
        /// 总条数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 数据列表
        /// </summary>
        public List<T> Items { get; set; } = new();

        /// <summary>
        /// 当前页码（从1开始）
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// 每页条数
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// 创建分页结果
        /// </summary>
        public static PageResult<T> Create(int totalCount, List<T> items, int pageIndex, int pageSize)
        {
            return new PageResult<T>
            {
                TotalCount = totalCount,
                Items = items,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
    }
}
