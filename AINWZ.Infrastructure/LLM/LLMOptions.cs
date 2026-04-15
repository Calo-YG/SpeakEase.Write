namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// LLM Provider 配置项。
/// </summary>
public sealed class LLMOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "LLM";

    /// <summary>
    /// OpenAI-compatible 网关基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

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
