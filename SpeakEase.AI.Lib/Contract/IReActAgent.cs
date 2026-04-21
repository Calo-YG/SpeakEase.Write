using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// ReAct 模式 Agent 接口，支持 Tool 注册、Skill 注册和 Pipeline Filter
    /// </summary>
    public interface IReActAgent
    {
        /// <summary>
        /// 注册工具执行器
        /// </summary>
        /// <param name="tool">工具执行器</param>
        void RegisterTool(IToolExecutor tool);

        /// <summary>
        /// 注册技能定义
        /// </summary>
        /// <param name="skill">技能定义</param>
        void RegisterSkill(SkillDefinition skill);

        /// <summary>
        /// 使用管道过滤器
        /// </summary>
        /// <param name="filter">管道过滤器</param>
        void UsePipelineFilter(IAgentPipelineFilter filter);

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
