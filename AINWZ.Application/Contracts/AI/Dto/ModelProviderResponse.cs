namespace SpeakEase.Write.Application.Contracts.AI.Dto;

/// <summary>
/// 模型提供商响应 DTO。
/// </summary>
public sealed class ModelProviderResponse
{
    /// <summary>
    /// 提供商实体标识。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 提供商标识，例如 "openai"、"anthropic"。
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 提供商展示名称，例如 "OpenAI"。
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 提供商说明。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// API 基础地址。
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreateAt { get; set; }
}
