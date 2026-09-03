using System.Runtime.CompilerServices;

using Microsoft.Extensions.Options;

using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Runtime;

/// <summary>
/// Chat 入口与 AgentLoop/Plan 执行之间的运行时门面。
/// 保留旧 CreationOrchestrator 作为内部兼容实现，后续可逐步替换其路由和上下文职责。
/// </summary>
public sealed class CreationRuntimeFacade(
    CreationOrchestrator orchestrator,
    IOptions<AgentRuntimeModeOptions> options = null) : ICreationRuntimeFacade
{
    private readonly AgentEventProjector _eventProjector = new();
    private readonly AgentRuntimeModeOptions _options = options?.Value ?? new AgentRuntimeModeOptions();

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        AgentRuntimeRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in ExecuteCoreAsync(
            request,
            _options.Mode,
            _options.EnableDynamicToolExposure,
            cancellationToken))
        {
            yield return chunk;
        }
    }

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        CreationRuntimeRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await foreach (var chunk in ExecuteCoreAsync(
            request.Request,
            string.IsNullOrWhiteSpace(request.RuntimeMode) ? _options.Mode : request.RuntimeMode,
            request.EnableDynamicToolExposure ?? _options.EnableDynamicToolExposure,
            cancellationToken))
        {
            yield return chunk;
        }
    }

    private async IAsyncEnumerable<AgentStreamChunk> ExecuteCoreAsync(
        AgentRuntimeRequest request,
        string runtimeMode,
        bool enableDynamicToolExposure,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mode = NormalizeMode(runtimeMode);

        long sequence = 0;
        var chunks = mode == "agent-loop"
            ? orchestrator.ExecuteRuntimeAsync(request, enableDynamicToolExposure, cancellationToken)
            : orchestrator.ExecuteAsync(request, cancellationToken);
        await foreach (var chunk in chunks)
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

    private static string NormalizeMode(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode) || string.Equals(mode, "legacy", StringComparison.OrdinalIgnoreCase))
            return "legacy";
        if (string.Equals(mode, "agent-loop", StringComparison.OrdinalIgnoreCase))
            return "agent-loop";
        throw new InvalidOperationException($"Unsupported AI runtime mode '{mode}'.");
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
