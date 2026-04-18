using AINWZ.Infrastructure.LLM.Options;
using AINWZ.Infrastructure.MutilCache;
using AINWZ.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpeakEase.Authorization.Authorization;

namespace AINWZ.Infrastructure.LLM.Providers;

/// <summary>
/// 基于当前用户激活配置动态解析 LLM 运行时选项。
/// 优先使用用户的激活配置（UserAiModelConfigEntity → AIModelDefinitionEntity），
/// 若用户无配置则回退到 appsettings.json 中的 LLMOptions 默认值。
/// 结果按用户维度通过 IMultiCacheService 缓存，避免每次请求查库。
/// </summary>
public sealed class CurrentLLMOptionsResolver : ICurrentLLMOptions
{
    private readonly AINWZDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly IOptions<LLMOptions> _fallbackOptions;
    private readonly IMultiCacheService _cache;

    public CurrentLLMOptionsResolver(
        AINWZDbContext dbContext,
        IUserContext userContext,
        IOptions<LLMOptions> fallbackOptions,
        IMultiCacheService cache)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _fallbackOptions = fallbackOptions;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<CurrentLLMOptions> GetCurrentOptionsAsync(CancellationToken cancellationToken = default)
    {
        var userId = _userContext.UserId;
        var cacheKey = CurrentLLMOptions.BuildCacheKey(userId);

        return await _cache.GetOrSetAsync<CurrentLLMOptions>(
            cacheKey,
            () => ResolveFromDatabaseAsync(userId, cancellationToken),
            memoryExpiry: TimeSpan.FromMinutes(5),
            redisExpiry: TimeSpan.FromMinutes(10));
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(string userId = null, CancellationToken cancellationToken = default)
    {
        userId ??= _userContext.UserId;
        var cacheKey = CurrentLLMOptions.BuildCacheKey(userId);
        await _cache.RemoveAsync(cacheKey);
    }

    /// <summary>
    /// 从数据库解析用户的 LLM 运行时选项。
    /// </summary>
    private async Task<CurrentLLMOptions> ResolveFromDatabaseAsync(string userId, CancellationToken cancellationToken)
    {
        // 查询用户激活的模型配置
        var activeConfig = await _dbContext.UserAiModelConfigs
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeConfig is null)
        {
            // 无用户配置，回退到配置文件默认值
            return FromFallbackOptions();
        }

        // 查询首选提供商
        var provider = await _dbContext.AIModelDefinitions
            .AsNoTracking()
            .Where(x => x.Id == activeConfig.ProviderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (provider is null)
        {
            return FromFallbackOptions();
        }

        // 查询备用提供商（如有）
        string fallbackApiBaseUrl = null;
        string fallbackModelName = null;
        if (!string.IsNullOrWhiteSpace(activeConfig.FallbackProviderId) && activeConfig.UseFallback)
        {
            var fallbackProvider = await _dbContext.AIModelDefinitions
                .AsNoTracking()
                .Where(x => x.Id == activeConfig.FallbackProviderId)
                .FirstOrDefaultAsync(cancellationToken);

            if (fallbackProvider is not null)
            {
                fallbackApiBaseUrl = fallbackProvider.ApiBaseUrl;
                fallbackModelName = activeConfig.FallbackModelName;
            }
        }

        var fallback = _fallbackOptions.Value;
        var fallbackModels = new List<string>();

        // 如果备用提供商与首选提供商相同，直接加备用模型名
        if (!string.IsNullOrWhiteSpace(fallbackModelName))
        {
            // 备用模型在不同提供商时，需要支持跨提供商降级（后续可扩展）
            // 目前备用模型在同一个 Provider 下使用 FallbackModels
            if (string.IsNullOrWhiteSpace(activeConfig.FallbackProviderId) ||
                activeConfig.FallbackProviderId == activeConfig.ProviderId)
            {
                fallbackModels.Add(fallbackModelName);
            }
        }

        return new CurrentLLMOptions
        {
            BaseUrl = provider.ApiBaseUrl,
            ApiKey = provider.ApiKey,
            DefaultModel = activeConfig.ModelName,
            FallbackModels = fallbackModels,
            TimeoutSeconds = fallback.TimeoutSeconds,
            ApiKeyHeaderName = fallback.ApiKeyHeaderName,
            ApiKeyHeaderPrefix = fallback.ApiKeyHeaderPrefix
        };
    }

    private CurrentLLMOptions FromFallbackOptions()
    {
        var fallback = _fallbackOptions.Value;
        return new CurrentLLMOptions
        {
            BaseUrl = fallback.BaseUrl,
            ApiKey = fallback.ApiKey,
            DefaultModel = fallback.DefaultModel,
            FallbackModels = fallback.FallbackModels,
            TimeoutSeconds = fallback.TimeoutSeconds,
            ApiKeyHeaderName = fallback.ApiKeyHeaderName,
            ApiKeyHeaderPrefix = fallback.ApiKeyHeaderPrefix
        };
    }
}
