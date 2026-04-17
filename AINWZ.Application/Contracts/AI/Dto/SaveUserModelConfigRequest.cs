namespace AINWZ.Application.Contracts.AI.Dto;

/// <summary>
/// 保存用户模型配置请求 DTO。
/// </summary>
public sealed class SaveUserModelConfigRequest
{
    /// <summary>
    /// 配置标识。为空时创建新配置，非空时更新已有配置。
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 配置名称，例如 "日常续写"、"深度分析"，同一用户下唯一。
    /// </summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>
    /// 首选提供商标识，指向 AIModelDefinitionEntity.Id。
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// 首选模型名称。
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 备用提供商标识，指向 AIModelDefinitionEntity.Id。
    /// </summary>
    public string FallbackProviderId { get; set; } = string.Empty;

    /// <summary>
    /// 备用模型名称。
    /// </summary>
    public string FallbackModelName { get; set; } = string.Empty;

    /// <summary>
    /// 是否允许自动降级到备用模型。
    /// </summary>
    public bool UseFallback { get; set; } = true;

    /// <summary>
    /// 模型配置偏好，例如 speed、quality。
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
}
