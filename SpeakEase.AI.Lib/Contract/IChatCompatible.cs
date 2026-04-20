using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent 视角的 LLM 后端接口。
    /// Agent 不关心 HTTP 请求、模型切换、Fallback 等 Infrastructure 细节，
    /// 只关心"我给你上下文，你给我回复"。
    /// </summary>
    public interface IChatCompatible
    {
        /// <summary>
        /// 请求 LLM 完成一次推理，返回完整响应。
        /// </summary>
        Task<AgentResponse> CompleteAsync(AgentRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 请求 LLM 以流式方式完成推理，逐步返回响应片段。
        /// </summary>
        IAsyncEnumerable<AgentStreamChunk> StreamAsync(AgentRequest request, CancellationToken cancellationToken = default);
    }
}
