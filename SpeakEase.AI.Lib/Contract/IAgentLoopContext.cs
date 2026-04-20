using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent 暴露给 Loop 策略的上下文能力。
    /// 策略通过此接口与 Agent 交互：请求预处理、工具执行、LLM 调用等。
    /// </summary>
    public interface IAgentLoopContext
    {
        /// <summary>
        /// LLM 后端，策略通过它发起 LLM 调用。
        /// </summary>
        IChatCompatible LLMBackend { get; }

        /// <summary>
        /// 预处理请求：注入工具定义、技能提示词等。
        /// </summary>
        Task<AgentRequest> PrepareRequestAsync(AgentRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 判断是否应执行工具调用（安全门控）。
        /// </summary>
        bool ShouldExecuteTools(AgentRequest request, AgentResponse response);

        /// <summary>
        /// 执行工具调用列表。
        /// </summary>
        Task<List<ToolResult>> ExecuteToolsAsync(List<ToolCall> toolCalls, CancellationToken cancellationToken);
    }
}
