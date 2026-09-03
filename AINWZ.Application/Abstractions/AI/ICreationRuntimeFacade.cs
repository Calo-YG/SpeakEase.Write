using SpeakEase.AI.Lib.Models;

namespace SpeakEase.Write.Application.Abstractions.AI;

public interface ICreationRuntimeFacade : IAgentOrchestrator
{
    IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        CreationRuntimeRequest request,
        CancellationToken cancellationToken = default);
}
