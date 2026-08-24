using SpeakEase.AI.Lib.Models;

namespace SpeakEase.Write.Application.Abstractions.AI;

public interface IAgentOrchestrator
{
    IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string sessionId,
        string userMessage,
        int maxIterations = 10,
        int? requestedMaxTokens = null,
        double? requestedTemperature = null,
        CancellationToken cancellationToken = default);
}
