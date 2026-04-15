namespace AINWZ.Infrastructure.Shared
{
    public class PageResult<T>
    {
        /// <summary>
        /// 总条数
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// 数据
        /// </summary>
        public List<T> Data { get; set; }

        /// <summary>
        /// 创建分页结果
        /// </summary>
        /// <param name="total"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static PageResult<T> Create(int total, List<T> data)
        {
            return new PageResult<T>
            {
                Total = total,
                Data = data
            };
        }
    }
}
