namespace SpeakEase.Write.Application.Contracts.Users.Dto;

/// <summary>
/// 用户模型配置响应 DTO。
/// 包含配置全部字段及通过关联查询的提供商展示名称。
/// </summary>
public sealed class UserModelConfigResponse
{
    /// <summary>
    /// 配置唯一标识。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 配置名称，例如 "日常续写"、"深度分析"。
    /// </summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>
    /// 首选提供商标识。
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// 首选提供商展示名称（通过关联 AIModelDefinitionEntity 获取）。
    /// </summary>
    public string ProviderLabel { get; set; } = string.Empty;

    /// <summary>
    /// 首选模型名称。
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 备用提供商标识。
    /// </summary>
    public string FallbackProviderId { get; set; } = string.Empty;

    /// <summary>
    /// 备用提供商展示名称（通过关联 AIModelDefinitionEntity 获取）。
    /// </summary>
    public string FallbackProviderLabel { get; set; } = string.Empty;

    /// <summary>
    /// 备用模型名称。
    /// </summary>
    public string FallbackModelName { get; set; } = string.Empty;

    /// <summary>
    /// 是否为当前激活配置。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 是否允许自动降级到备用模型。
    /// </summary>
    public bool UseFallback { get; set; }

    /// <summary>
    /// 模型配置偏好。
    /// </summary>
    public string Preference { get; set; } = string.Empty;

    /// <summary>
    /// 模型说明。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 预估调用成本。
    /// </summary>
    public decimal EstimateCost { get; set; }

    /// <summary>
    /// 上下文窗口大小。
    /// </summary>
    public int ContextWindow { get; set; }

    /// <summary>
    /// 最大输出 token 数。
    /// </summary>
    public int MaxOutputTokens { get; set; }

    /// <summary>
    /// 是否支持流式输出。
    /// </summary>
    public bool SupportsStreaming { get; set; }

    /// <summary>
    /// 是否支持工具调用。
    /// </summary>
    public bool SupportsToolCall { get; set; }

    /// <summary>
    /// 能力标签集合。
    /// </summary>
    public List<string> CapabilityTags { get; set; } = new();

    /// <summary>
    /// 最近同步时间。
    /// </summary>
    public DateTime LastSyncedAt { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreateAt { get; set; }
}
