using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AINWZ.Infrastructure.MutilCache
{
    public class MultiCacheService(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ILogger<MultiCacheService> logger) : IMultiCacheService
    {

        // 每个key一个锁，防止缓存击穿
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();

        public async Task<TCache> GetOrSetAsync<TCache>(
            string key,
            Func<Task<TCache>> func,
            Action error = null,
            TimeSpan? memoryExpiry = null,
            TimeSpan? redisExpiry = null,
            int jitterSeconds = 30)
        {
            // 1. 读L1
            if (memoryCache.TryGetValue(key, out TCache value) && value is not null)
            {
                logger.LogDebug("L1命中: {Key}", key);
                return value;
            }

            var semaphore = locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                if (memoryCache.TryGetValue(key, out value) && value is not null)
                {
                    logger.LogDebug("L1命中(锁内): {Key}", key);
                    return value;
                }

                try
                {
                    var redisValue = await distributedCache.GetStringAsync(key);
                    if (!string.IsNullOrEmpty(redisValue))
                    {
                        logger.LogDebug("L2命中: {Key}", key);
                        value = JsonSerializer.Deserialize<TCache>(redisValue);

                        SetMemoryCache(key, value, memoryExpiry, jitterSeconds);
                        return value;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Redis读取失败: {Key}", key);
                }


                logger.LogDebug("回源: {Key}", key);
                var result = await func();

                if (result is null)
                {
                    return default!;
                }


                await SetCacheAsync(key, result, memoryExpiry, redisExpiry, jitterSeconds);
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "失败: {Key}", key);
                error?.Invoke();
                throw;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 核心：过期时间加随机偏移，打散过期时间点
        /// </summary>
        private static TimeSpan AddJitter(TimeSpan baseExpiry, int jitterSeconds)
        {
            if (jitterSeconds <= 0)
                return baseExpiry;

            var jitter = Random.Shared.Next(0, jitterSeconds);
            return baseExpiry + TimeSpan.FromSeconds(jitter);
        }

        /// <summary>
        /// 刷新缓存：删除旧缓存，重新加载数据
        /// </summary>
        public async Task RefreshAsync<TCache>(
            string key,
            TCache cache,
            TimeSpan? memoryExpiry = null,
            TimeSpan? redisExpiry = null,
            int jitterSeconds = 30)
        {
            var semaphore = locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();

            try
            {
                memoryCache.Remove(key);
                try
                {
                    await distributedCache.RemoveAsync(key);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Redis删除失败: {Key}", key);
                }

                if (memoryCache.TryGetValue(key, out TCache value) && value is not null)
                {
                    logger.LogDebug("刷新时命中内存缓存(其他线程已刷新): {Key}", key);
                }

                logger.LogDebug("强制刷新回源: {Key}", key);

                await SetCacheAsync(key, cache, memoryExpiry, redisExpiry, jitterSeconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "刷新失败: {Key}", key);

                throw;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// 删除缓存（L1 + L2）
        /// </summary>
        public async Task RemoveAsync(string key)
        {
            memoryCache.Remove(key);

            try
            {
                await distributedCache.RemoveAsync(key);
                logger.LogDebug("缓存已删除: {Key}", key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis删除失败: {Key}", key);
            }
        }

        private async Task SetCacheAsync<TCache>(
            string key,
            TCache value,
            TimeSpan? memoryExpiry,
            TimeSpan? redisExpiry,
            int jitterSeconds)
        {
            // L1：绝对过期 + 偏移
            SetMemoryCache(key, value, memoryExpiry, jitterSeconds);

            // L2：绝对过期 + 偏移（失败不影响）
            try
            {
                var expiry = AddJitter(redisExpiry ?? TimeSpan.FromMinutes(30), jitterSeconds);
                var json = JsonSerializer.Serialize(value);

                await distributedCache.SetStringAsync(key, json, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiry
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis写入失败: {Key}", key);
            }
        }

        private void SetMemoryCache<TCache>(string key, TCache value, TimeSpan? expiry, int jitterSeconds)
        {
            var finalExpiry = AddJitter(expiry ?? TimeSpan.FromMinutes(5), jitterSeconds);

            // 只用绝对过期，避免和滑动过期冲突
            memoryCache.Set(key, value, finalExpiry);
        }
    }
}
