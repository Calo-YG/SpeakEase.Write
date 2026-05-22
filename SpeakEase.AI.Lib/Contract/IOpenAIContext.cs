namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// OpenAI 交互上下文：按用户维度动态解析 LLM 配置并缓存。
    /// </summary>
    public interface IOpenAIContext
    {
        /// <summary>
        /// API 密钥。
        /// </summary>
        string ApiKey { get; }

        /// <summary>
        /// API 基础地址。
        /// </summary>
        string Url { get; }

        /// <summary>
        /// 模型名称。
        /// </summary>
        string Model { get; }

        int MaxOutputTokens { get; }

        int ContextWindow { get; }

        /// <summary>
        /// 动态解析当前用户的 LLM 配置并缓存。
        /// </summary>
        Task ResolveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 使指定用户的缓存失效。
        /// </summary>
        Task InvalidateAsync(string userId = null, CancellationToken cancellationToken = default);
    }
}
