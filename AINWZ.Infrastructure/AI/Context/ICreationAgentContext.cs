namespace SpeakEase.Write.Infrastructure.AI.Context
{
    public interface ICreationAgentContext
    {
        /// <summary>
        /// 构建当前会话上下文
        /// </summary>
        /// <returns></returns>
        Task<AgentContext> BuildContext();
    }
}
