using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// IChatAgent 切面过滤器，采用中间件管道模式。
    /// 类似 ASP.NET Core 的中间件，可以在 Agent 调用前后插入横切逻辑。
    /// 典型用途：日志记录、安全熔断、上下文注入、缓存等。
    /// </summary>
    public interface IChatAgentFilter
    {
        /// <summary>
        /// 拦截 Agent 调用，在调用 next 前后可执行自定义逻辑。
        /// </summary>
        /// <param name="context">Agent 运行时上下文，携带请求、迭代信息、跨 Filter 共享数据等。</param>
        /// <param name="next">管道中下一个 Filter 或最终的 Agent 调用。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        Task<AgentResponse> InvokeAsync(
            AgentContext context,
            Func<AgentContext, CancellationToken, Task<AgentResponse>> next,
            CancellationToken cancellationToken = default);
    }
}
