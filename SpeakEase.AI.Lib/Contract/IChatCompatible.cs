using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent 视角的 LLM 后端接口。
    /// Agent 不关心 HTTP 请求、模型切换、Fallback 等 Infrastructure 细节，
    /// 只关心"我给你 OpenAI-compatible 请求，你给我标准响应"。
    /// 此接口仅负责单次 LLM 调用（含模型回退），
    /// 不包含工具调度 / 技能注入 / Agent Loop 等策略逻辑。
    /// </summary>
    public interface IChatCompatible
    {
        /// <summary>
        /// 非流式调用：发送 OpenAI-compatible chat completions 请求，返回完整响应。
        /// 内部自动处理模型回退逻辑。  
        /// </summary>
        /// <param name="request">OpenAI 格式的请求体。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>完整的 OpenAI 格式响应。</returns>
        Task<ChatCompletionResponse> ChatAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 流式调用：发送 OpenAI-compatible chat completions 请求，逐片段返回响应增量。
        /// 内部自动处理模型回退逻辑。
        /// </summary>
        /// <param name="request">OpenAI 格式的请求体（内部会强制设置 stream=true）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>流式响应片段的异步枚举。</returns>
        IAsyncEnumerable<ChatCompletionStreamChunk> StreamAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default);
    }
}
