using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent 工具调用能力接口（可选）。
    /// 实现此接口的 Agent 可以声明自己支持的工具并执行工具调用。
    /// </summary>
    public interface IToolCapable
    {
        /// <summary>
        /// 该 Agent 注册的工具定义列表。
        /// </summary>
        IReadOnlyList<ToolDefinition> Tools { get; }

        /// <summary>
        /// 注册工具定义。Agent 可以通过此方法声明自己支持的工具，供 AgentExecutor 在执行过程中调用。
        /// </summary>
        /// <param name="tool"></param>
        void RegisterTool(ToolDefinition tool);

        /// <summary>
        /// 执行一次工具调用。
        /// </summary>
        /// <param name="toolCall"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken);


    }
}
