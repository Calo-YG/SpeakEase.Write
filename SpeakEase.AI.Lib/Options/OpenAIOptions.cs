namespace SpeakEase.AI.Lib.Options
{
    /// <summary>
    /// LLM 后端配置选项。
    /// 可由消费方通过 IOptions 模式注入，也可由代码直接构造。
    /// </summary>
    public sealed class OpenAIOptions
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
        /// 备用模型列表（主模型失败时依次回退）。
        /// </summary>
        public List<string> FallbackModels { get; set; } = new();

        /// <summary>
        /// 请求超时时间（秒）。
        /// </summary>
        public int TimeoutSeconds { get; set; } = 120;

        /// <summary>
        /// 自定义 API Key 请求头名称。为空时使用 Authorization: Bearer。
        /// </summary>
        public string ApiKeyHeaderName { get; set; }

        /// <summary>
        /// 自定义 API Key 请求头前缀（如 "Bearer"）。为空则直接写 key。
        /// </summary>
        public string ApiKeyHeaderPrefix { get; set; }
    }
}
