using SpeakEase.AI.Lib.Models;

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
        /// 执行一次工具调用。
        /// </summary>
        Task<ToolResult> ExecuteToolAsync(ToolCall call, CancellationToken cancellationToken = default);
    }
}
