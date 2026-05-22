namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// OpenAI 交互上下文：按用户维度动态解析 LLM 配置并缓存。
    /// </summary>
    public interface IOpenAIContext
    {
        /// <summary>
        /// API 密钥（Bearer Token）
        /// </summary>
        string ApiKey { get; }

        /// <summary>
        /// API 基础地址（如 https://api.openai.com/v1）
        /// </summary>
        string Url { get; }

        /// <summary>
        /// 模型名称（如 gpt-4o、deepseek-chat）
        /// </summary>
        string Model { get; }

        /// <summary>
        /// 单次请求最大输出 Token 数上限
        /// </summary>
        int MaxOutputTokens { get; }

        /// <summary>
        /// 模型上下文窗口大小（Token 数）
        /// </summary>
        int ContextWindow { get; }

        /// <summary>
        /// 动态解析当前用户的 LLM 配置（API Key、模型、Base URL 等）并缓存，避免每次调用都查询。
        /// </summary>
        Task ResolveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 使指定用户的 LLM 配置缓存失效，下次调用 ResolveAsync 时将重新加载。
        /// 参数为 null 时失效当前用户的缓存。
        /// </summary>
        Task InvalidateAsync(string userId = null, CancellationToken cancellationToken = default);
    }
}
