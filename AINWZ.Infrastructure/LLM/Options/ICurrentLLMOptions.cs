namespace AINWZ.Infrastructure.LLM.Options;

/// <summary>
/// 当前 LLM 运行时选项，不再依赖配置文件，而是从用户自定义模型配置动态获取。
/// 每次请求根据当前用户的激活配置（UserAiModelConfigEntity → AIModelDefinitionEntity）
/// 解析出对应的 BaseUrl、ApiKey、Model 等信息。
/// </summary>
public sealed class CurrentLLMOptions
{
    /// <summary>
    /// 缓存 Key 前缀。
    /// </summary>
    public const string CacheKeyPrefix = "CurrentLLMOptions:";

    /// <summary>
    /// 构建指定用户的缓存 Key。
    /// </summary>
    public static string BuildCacheKey(string userId) => $"{CacheKeyPrefix}{userId}";

    /// <summary>
    /// OpenAI-compatible 网关基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API Key。
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 默认模型标识。
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// 默认备用模型列表。
    /// </summary>
    public List<string> FallbackModels { get; set; } = new();

    /// <summary>
    /// 请求超时时间（秒）。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// 自定义请求头名称。
    /// </summary>
    public string ApiKeyHeaderName { get; set; }

    /// <summary>
    /// 自定义请求头前缀；为空则直接写 key。
    /// </summary>
    public string ApiKeyHeaderPrefix { get; set; }
}

/// <summary>
/// 当前 LLM 运行时选项解析接口。
/// 根据当前用户的激活模型配置动态提供 BaseUrl/ApiKey/Model 等信息。
/// </summary>
public interface ICurrentLLMOptions
{
    /// <summary>
    /// 获取当前用户的 LLM 运行时选项。
    /// 优先使用用户的激活配置，若无则回退到配置文件默认值。
    /// 结果按用户维度缓存，配置变更时通过 <see cref="InvalidateAsync"/> 失效。
    /// </summary>
    Task<CurrentLLMOptions> GetCurrentOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 使指定用户的 LLM 运行时选项缓存失效。
    /// 在用户模型配置发生变更（创建/更新/激活/删除）时调用。
    /// </summary>
    /// <param name="userId">用户标识；为 null 时使用当前用户。</param>
    Task InvalidateAsync(string userId = null, CancellationToken cancellationToken = default);
}
