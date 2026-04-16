namespace AINWZ.Infrastructure.MutilCache
{
    /// <summary>
    /// 多级缓存
    /// </summary>
    public interface IMultiCacheService
    {
        /// <summary>
        /// 获取或设置缓存
        /// </summary>
        /// <typeparam name="TCache"></typeparam>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="error"></param>
        /// <param name="memoryExpiry"></param>
        /// <param name="redisExpiry"></param>
        /// <param name="jitterSeconds"></param>
        /// <returns></returns>
        public Task<TCache> GetOrSetAsync<TCache>(
            string key,
            Func<Task<TCache>> func,
            Action error = null,
            TimeSpan? memoryExpiry = null,
            TimeSpan? redisExpiry = null,
            int jitterSeconds = 30);

        /// <summary>
        /// 刷新缓存
        /// </summary>
        /// <typeparam name="TCache"></typeparam>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="error"></param>
        /// <param name="memoryExpiry"></param>
        /// <param name="redisExpiry"></param>
        /// <param name="jitterSeconds"></param>
        /// <returns></returns>
        public Task RefreshAsync<TCache>(
            string key,
            TCache cache,
            TimeSpan? memoryExpiry = null,
            TimeSpan? redisExpiry = null,
            int jitterSeconds = 30);

        /// <summary>
        /// 删除缓存
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Task RemoveAsync(string key);
    }
}
