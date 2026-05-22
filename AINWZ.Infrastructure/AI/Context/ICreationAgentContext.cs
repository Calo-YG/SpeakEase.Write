namespace SpeakEase.Write.Infrastructure.AI.Context;

// Agent 上下文构建接口：根据作品/会话/Agent 类型构建 LLM 调用所需的完整上下文
public interface ICreationAgentContext
{
    Task<AgentContext> BuildContextAsync(
        string workId,
        string sessionId,
        string agentName,
        string primaryModel,
        bool includeMemory,
        bool filterHistory,
        int contextWindowTokens,
        CancellationToken cancellationToken = default);
}
