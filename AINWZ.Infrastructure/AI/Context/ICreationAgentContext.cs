namespace SpeakEase.Write.Infrastructure.AI.Context;

public interface ICreationAgentContext
{
    Task<AgentContext> BuildContext(string workId, CancellationToken cancellationToken = default);
}
