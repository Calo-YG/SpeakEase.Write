using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent Loop 执行策略抽象。
    /// 不同的策略决定 Agent 如何迭代：ReAct 循环、单轮调用、Plan-and-Execute 等。
    /// </summary>
    public interface IReActStrategy
    {
        /// <summary>
        /// 执行 Agent Loop 的非流式模式。
        /// </summary>
        /// <param name="context">Agent 上下文能力，策略通过它与 Agent 交互。</param>
        /// <param name="request">原始请求。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        Task<AgentResponse> ExecuteAsync(IAgentLoopContext context, AgentRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 执行 Agent Loop 的流式模式。
        /// </summary>
        /// <param name="context">Agent 上下文能力，策略通过它与 Agent 交互。</param>
        /// <param name="request">原始请求。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        IAsyncEnumerable<AgentStreamChunk> StreamAsync(IAgentLoopContext context, AgentRequest request, CancellationToken cancellationToken);
    }
}
