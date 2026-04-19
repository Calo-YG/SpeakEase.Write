using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// 单轮调用策略：只调用一次 LLM，不做任何循环。
    /// 
    /// 适用于：
    /// - 纯对话 Agent（无工具调用需求）
    /// - 内容生成（翻译、摘要、续写等）
    /// - 工具调用由外层编排，Agent 只负责单次推理的场景
    /// 
    /// 与 ReAct + MaxIterations=1 的区别：
    /// - 语义更清晰，明确表达"不做循环"的意图
    /// - 不走 Loop 逻辑中的工具检查、迭代追踪等开销
    /// - 始终设置 Iterations=1、StopReason="completed"
    /// </summary>
    public class SingleTurnStrategy : IAgentLoopStrategy
    {
        /// <inheritdoc />
        public async Task<AgentResponse> ExecuteAsync(
            IAgentLoopContext context,
            AgentRequest request,
            CancellationToken cancellationToken)
        {
            // 单轮策略不执行工具循环：提前禁用工具，避免 PrepareRequestAsync 做无用的工具注入。
            // 技能提示词注入仍有价值，所以仍需调用 PrepareRequestAsync。
            request.EnableToolDispatch = false;
            request.Tools = new List<ToolDefinition>();

            var prepared = await context.PrepareRequestAsync(request, cancellationToken);

            // 单次 LLM 调用
            var response = await context.LLMBackend.CompleteAsync(prepared, cancellationToken);

            // 填充元数据
            response.Iterations = 1;
            response.StopReason = "completed";
            response.ConversationHistory = prepared.Messages;

            return response;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<AgentStreamChunk> StreamAsync(
            IAgentLoopContext context,
            AgentRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // 单轮策略不执行工具循环：提前禁用工具
            request.EnableToolDispatch = false;
            request.Tools = new List<ToolDefinition>();

            // 预处理请求（技能提示词注入仍有效，工具注入被跳过）
            var prepared = await context.PrepareRequestAsync(request, cancellationToken);

            // 单次流式 LLM 调用，直接透传所有 chunk
            await foreach (var chunk in context.LLMBackend.StreamAsync(prepared, cancellationToken).WithCancellation(cancellationToken))
            {
                chunk.Iteration = 1;
                yield return chunk;
            }

            // 发送结束标记
            yield return new AgentStreamChunk
            {
                Type = "iteration_end",
                Iteration = 1,
                StopReason = "completed"
            };
        }
    }
}
