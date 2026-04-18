namespace AINWZ.Infrastructure.LLM.Options;

/// <summary>
/// 搜索提供商类型。
/// </summary>
public enum SearchProvider
{
    /// <summary>通用网关（POST JSON）</summary>
    Generic,

    /// <summary>Microsoft Bing Web Search API</summary>
    Bing
}

/// <summary>
/// 内置 web_search 工具配置。
/// </summary>
public sealed class ToolSearchOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "ToolSearch";

    // ==================== 提供商 ====================

    /// <summary>
    /// 搜索提供商类型；默认 Bing。
    /// </summary>
    public SearchProvider Provider { get; set; } = SearchProvider.Bing;

    // ==================== 基础连接 ====================

    /// <summary>
    /// 搜索网关地址；为空时 web_search 工具不可用。
    /// Bing 默认: https://api.bing.microsoft.com/v7.0/search
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// 搜索网关鉴权令牌。Bing 为 Ocp-Apim-Subscription-Key 值。
    /// </summary>
    public string ApiKey { get; set; }
    
    /// <summary>
    /// 鉴权请求头名称；Bing 默认 Ocp-Apim-Subscription-Key，Generic 默认 Authorization。
    /// </summary>
    public string ApiKeyHeaderName { get; set; } = "Ocp-Apim-Subscription-Key";
    
    /// <summary>
    /// 鉴权请求头前缀；Bing 默认为空（直接写 Key），Generic 默认 Bearer。
    /// </summary>
    public string ApiKeyHeaderPrefix { get; set; } = "";

    // ==================== 请求控制 ====================

    /// <summary>
    /// 搜索请求超时时间（秒）；默认 15 秒。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// 请求失败后的重试次数；默认 0（不重试）。
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 重试间隔（毫秒）；默认 1000ms。
    /// </summary>
    public int RetryIntervalMs { get; set; } = 1000;

    // ==================== 结果限制 ====================

    /// <summary>
    /// 默认返回搜索结果数；默认 5。
    /// </summary>
    public int DefaultMaxResults { get; set; } = 5;

    /// <summary>
    /// 允许的最大搜索结果数上限；默认 10，取值范围 [1, 20]。
    /// </summary>
    public int MaxResultsLimit { get; set; } = 10;

    /// <summary>
    /// 返回内容的最大字符数，超出时截断并标记 truncated=true；默认 4000。
    /// </summary>
    public int MaxContentLength { get; set; } = 4000;

    // ==================== 搜索行为 ====================

    /// <summary>
    /// 搜索语言区域（如 zh-CN、en-US）；为空则由网关决定。
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// 搜索国家/地区代码（如 CN、US）；为空则由网关决定。
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// 是否启用安全搜索（过滤成人内容）；默认 true。
    /// </summary>
    public bool SafeSearch { get; set; } = true;

    /// <summary>
    /// 搜索结果缓存时长（秒）；0 表示不缓存，默认 0。
    /// </summary>
    public int CacheTtlSeconds { get; set; }
}
