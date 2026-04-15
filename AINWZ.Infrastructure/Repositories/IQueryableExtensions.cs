using AINWZ.Infrastructure.Shared;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AINWZ.Infrastructure.Repositories
{
    public static class IQueryableExtensions
    {
        /// <summary>
        /// whereif
        /// </summary>
        /// <param name="source"></param>
        /// <param name="condition"></param>
        /// <param name="predicate"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, bool condition, Expression<Func<T, bool>> predicate)
        {
            return condition ? source.Where(predicate) : source;
        }

        /// <summary>
        /// 分页
        /// </summary>
        /// <param name="source"></param>
        /// <param name="pagination"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static PageResult<T> ToPageResult<T>(this IQueryable<T> source, Pagination pagination)
        {
            var total = source.Count();
            var data = source.Skip((pagination.Page - 1) * pagination.PageSize).Take(pagination.PageSize).ToList();
            return PageResult<T>.Create(total, data);
        }

        /// <summary>
        /// 异步分页
        /// </summary>
        /// <param name="source"></param>
        /// <param name="pagination"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async Task<PageResult<T>> ToPageResultAsync<T>(this IQueryable<T> source, Pagination pagination)
        {
            var total = await source.CountAsync();
            var data = await source.Skip((pagination.Page - 1) * pagination.PageSize).Take(pagination.PageSize).ToListAsync();
            return PageResult<T>.Create(total, data);
        }
    }
}
