using SpeakEase.Write.Infrastructure.AI.Memory;

namespace SpeakEase.Write.Infrastructure.AI.Context
{
    /// <summary>
    /// 单轮对话中使用到上下文类（单次请求）
    /// </summary>
    /// <param name="memoryProvider">记忆提供程序</param>
    public sealed class CreationAgentContext(IMemoryProvider memoryProvider) : ICreationAgentContext
    {
        public Task<AgentContext> BuildContext()
        {
            throw new NotImplementedException();
        }
    }
}
