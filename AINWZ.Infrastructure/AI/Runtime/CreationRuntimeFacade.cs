using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Runtime;

/// <summary>
/// Chat 入口与 AgentLoop/Plan 执行之间的运行时门面。
/// 保留旧 CreationOrchestrator 作为内部兼容实现，后续可逐步替换其路由和上下文职责。
/// </summary>
public sealed class CreationRuntimeFacade(CreationOrchestrator orchestrator) : IAgentOrchestrator
{
    public IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        return orchestrator.ExecuteAsync(request, cancellationToken);
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
        return orchestrator.ExecuteAsync(
            workId,
            sessionId,
            userMessage,
            maxIterations,
            requestedMaxTokens,
            requestedTemperature,
            cancellationToken);
    }
}
