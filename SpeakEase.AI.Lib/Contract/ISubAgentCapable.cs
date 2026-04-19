using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent SubAgent 能力接口（可选）。
    /// 实现此接口的 Agent 可以动态创建子 Agent 来执行子任务，
    /// 实现上下文隔离——主 Agent 只接收摘要结果，子 Agent 的完整执行过程被隔离和丢弃。
    /// 
    /// 设计参考 nanobot SubAgent：
    /// - 子 Agent 在独立上下文中执行，复用主 Agent 的 LLM 后端
    /// - 子 Agent 拥有受限的工具集和迭代次数
    /// - 执行完毕后结果摘要回传给主 Agent，子 Agent 上下文即丢弃
    /// </summary>
    public interface ISubAgentCapable
    {
        /// <summary>
        /// 创建并执行一个子 Agent。
        /// 子 Agent 在独立上下文中运行，完成后返回结果摘要。
        /// </summary>
        /// <param name="task">子 Agent 要执行的任务描述。</param>
        /// <param name="systemPrompt">子 Agent 的系统提示词。为空时使用任务描述作为提示词。</param>
        /// <param name="allowedToolNames">子 Agent 可用的工具名称列表。为空时使用主 Agent 的全部工具。</param>
        /// <param name="maxIterations">子 Agent 的最大迭代次数。为空时使用默认值。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>子 Agent 的执行结果。</returns>
        Task<SubAgentResult> SpawnAsync(string task, string systemPrompt = null, List<string> allowedToolNames = null, int? maxIterations = null, CancellationToken cancellationToken = default);
    }
}
