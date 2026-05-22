namespace SpeakEase.Write.Infrastructure.AI.Context;

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
