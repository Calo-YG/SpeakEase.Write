using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// ReAct 模式 Agent 接口。
    /// 支持：非流式/流式执行、多轮 Tool/Skill 调用循环、历史对话管理。
    /// </summary>
    public interface IReActAgent
    {
        /// <summary>
        /// 非流式执行 Agent 请求
        /// </summary>
        /// <param name="request">Agent 请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>Agent 响应</returns>
        Task<AgentResponse> ExecuteAsync(
            AgentRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 流式执行 Agent 请求
        /// </summary>
        /// <param name="request">Agent 请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>Agent 流式响应片段的异步枚举</returns>
        IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
            AgentRequest request,
            CancellationToken cancellationToken = default);
    }
}
