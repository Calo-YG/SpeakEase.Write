using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent 管道过滤器，在每次 LLM 调用前后执行切面逻辑
    /// </summary>
    public interface IAgentPipelineFilter
    {
        /// <summary>
        /// 执行管道过滤逻辑
        /// </summary>
        /// <param name="request">聊天完成请求</param>
        /// <param name="context">Agent 管道上下文</param>
        /// <param name="next">下一个管道委托</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>聊天完成响应</returns>
        Task<ChatCompletionResponse> InvokeAsync(
            Func<AgentPipelineContext,Task> next,
            CancellationToken cancellationToken = default);
    }
}
