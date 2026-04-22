using SpeakEase.Write.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.MutilCache;

namespace SpeakEase.Write.Infrastructure.AI;

/// <summary>
/// 按用户维度动态解析 LLM 配置，多级缓存避免每次查库。
/// </summary>
public sealed class OpenAIContext(
    IUserContext user,
    SpeakEaseDbContext db,
    IMultiCacheService cache,
    IConfiguration cfg,
    ILogger<OpenAIContext> log) : IOpenAIContext
{
    private const string CachePrefix = "LLM:Ctx:";
    private bool _resolved;

    public string ApiKey { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task ResolveAsync(CancellationToken ct = default)
    {
        if (_resolved) return;

        var userId = user.UserId;
        var c = await cache.GetOrSetAsync(
            $"{CachePrefix}{userId}",
            () => ResolveCoreAsync(userId, ct),
            memoryExpiry: TimeSpan.FromMinutes(5),
            redisExpiry: TimeSpan.FromMinutes(10));

        Url = c.Url;
        ApiKey = c.ApiKey;
        Model = c.Model;
        _resolved = true;

        log.LogDebug("OpenAIContext resolved: User={UserId}, Model={Model}", userId, Model);
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(string userId = null, CancellationToken ct = default)
        => await cache.RemoveAsync($"{CachePrefix}{userId ?? user.UserId}");

    private async Task<LLMConfig> ResolveCoreAsync(string userId, CancellationToken ct)
    {
        var row = await db.UserAiModelConfigs
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Join(db.AIModelDefinitions, c => c.ProviderId, p => p.Id,
                (c, p) => new { c.ModelName, p.ApiBaseUrl, p.ApiKey })
            .FirstOrDefaultAsync(ct);

        if (row is not null)
            return new LLMConfig(row.ApiBaseUrl, row.ApiKey, row.ModelName);

        var s = cfg.GetSection("LLM");
        return new LLMConfig(
            s["BaseUrl"] ?? "https://api.openai.com/v1/",
            s["ApiKey"] ?? string.Empty,
            s["DefaultModel"] ?? "gpt-4o-mini");
    }

    private sealed record LLMConfig(string Url, string ApiKey, string Model);
}
