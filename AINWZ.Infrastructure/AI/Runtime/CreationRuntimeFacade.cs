using System.Runtime.CompilerServices;

using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Runtime;

/// <summary>
/// Chat 入口与 AgentLoop/Plan 执行之间的运行时门面。
/// 保留旧 CreationOrchestrator 作为内部兼容实现，后续可逐步替换其路由和上下文职责。
/// </summary>
public sealed class CreationRuntimeFacade(CreationOrchestrator orchestrator) : IAgentOrchestrator
{
    private readonly AgentEventProjector _eventProjector = new();

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        AgentRuntimeRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        long sequence = 0;
        await foreach (var chunk in orchestrator.ExecuteAsync(request, cancellationToken))
        {
            var runtimeEvent = new AgentEvent
            {
                RunId = request.RunId,
                StepId = string.IsNullOrWhiteSpace(chunk.StepId) ? "runtime" : chunk.StepId,
                Sequence = ++sequence,
                Type = chunk.Type ?? string.Empty,
                Payload = chunk
            };
            yield return _eventProjector.ProjectToSse(runtimeEvent);
        }
    }

    public IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId,
        string sessionId,
        string userMessage,
        int maxIterations = 10,
        int? requestedMaxTokens = null,
        double? requestedTemperature = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(new AgentRuntimeRequest
        {
            WorkId = workId,
            SessionId = sessionId,
            UserMessage = userMessage,
            MaxIterations = maxIterations,
            MaxTokens = requestedMaxTokens,
            Temperature = requestedTemperature,
            EnableAutoToolDispatch = true
        }, cancellationToken);
    }
}
