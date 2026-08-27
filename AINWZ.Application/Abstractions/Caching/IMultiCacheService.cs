namespace SpeakEase.Write.Application.Abstractions.Caching;

public interface IMultiCacheService
{
    Task<TCache> GetOrSetAsync<TCache>(
        string key,
        Func<Task<TCache>> func,
        Action error = null,
        TimeSpan? memoryExpiry = null,
        TimeSpan? redisExpiry = null,
        int jitterSeconds = 30);

    Task RefreshAsync<TCache>(
        string key,
        TCache cache,
        TimeSpan? memoryExpiry = null,
        TimeSpan? redisExpiry = null,
        int jitterSeconds = 30);

    Task RemoveAsync(string key);
}
