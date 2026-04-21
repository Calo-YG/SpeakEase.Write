using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// LLM 单轮交互策略：封装与 LLM 的一次请求/响应交互，
    /// 使 ReActAgent 只关注循环编排逻辑，不感知底层协议细节。
    /// </summary>
    public interface IChatCompatible
    {
        /// <summary>
        /// 非流式单轮交互
        /// </summary>
        /// <param name="context">LLM 交互上下文（模型、温度等不变配置）</param>
        /// <param name="messages">当前消息列表</param>
        /// <param name="tools">可用工具定义列表，无工具时传 null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>本轮 LLM 交互结果</returns>
        Task<LLMTurnResult> ChatAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 流式单轮交互：实时输出增量片段，轮次结束时输出最终结果
        /// </summary>
        /// <param name="context">LLM 交互上下文（模型、温度等不变配置）</param>
        /// <param name="messages">当前消息列表</param>
        /// <param name="tools">可用工具定义列表，无工具时传 null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>流式增量片段的异步枚举</returns>
        IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default);
    }
}
