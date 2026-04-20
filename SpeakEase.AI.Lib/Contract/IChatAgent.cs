using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent 核心对话能力接口。
    /// 所有具备对话能力的 Agent 必须实现此接口。
    /// </summary>
    public interface IChatAgent
    {
        /// <summary>
        /// 执行一次对话请求，返回完整响应。
        /// </summary>
        Task<AgentResponse> ChatAsync(AgentRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 以流式方式执行对话请求，逐步返回响应片段。
        /// </summary>
        IAsyncEnumerable<AgentStreamChunk> StreamAsync(AgentRequest request, CancellationToken cancellationToken = default);
    }
}
