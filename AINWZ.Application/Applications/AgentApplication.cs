using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Snapshot;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Application.Applications;

public sealed class AgentApplication : IAgentApplication
{
    private readonly CreationOrchestrator _orchestrator;
    private readonly ISnapshotService _snapshotService;
    private readonly ICreationSessionManager _sessionManager;

    public AgentApplication(
        CreationOrchestrator orchestrator,
        ISnapshotService snapshotService,
        ICreationSessionManager sessionManager)
    {
        _orchestrator = orchestrator;
        _snapshotService = snapshotService;
        _sessionManager = sessionManager;
    }

    public async Task<AgentResponse> ChatAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default)
    {
        var workId = request.WorkId ?? string.Empty;
        var firstUserMessage = request.Messages?.FirstOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

        var correlationId = Guid.NewGuid().ToString();
        var sessionResult = await _sessionManager.GetActiveSessionAsync(workId);
        string sessionId = sessionResult.Data?.SessionId ?? (await _sessionManager.StartSessionAsync(workId)).Data?.SessionId;

        if (!string.IsNullOrWhiteSpace(workId))
            await _snapshotService.CaptureBeforeSnapshotAsync(workId, correlationId);

        var response = new AgentResponse
        {
            Content = string.Empty,
            StopReason = "completed"
        };

        var contentParts = new List<string>();

        await foreach (var chunk in _orchestrator.ExecuteAsync(
            workId, firstUserMessage, cancellationToken))
        {
            if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                contentParts.Add(chunk.Content);
        }

        response.Content = string.Join(string.Empty, contentParts);

        if (!string.IsNullOrWhiteSpace(workId))
            await _snapshotService.CaptureAfterSnapshotAsync(workId, correlationId);

        if (sessionId != null)
            await _sessionManager.RecordTurnAsync(sessionId);

        return response;
    }

    public async IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(
        AgentChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var workId = request.WorkId ?? string.Empty;
        var firstUserMessage = request.Messages?.FirstOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

        var correlationId = Guid.NewGuid().ToString();
        var sessionResult = await _sessionManager.GetActiveSessionAsync(workId);
        string sessionId = sessionResult.Data?.SessionId
                           ?? (await _sessionManager.StartSessionAsync(workId)).Data?.SessionId;

        if (!string.IsNullOrWhiteSpace(workId))
            await _snapshotService.CaptureBeforeSnapshotAsync(workId, correlationId);

        await foreach (var chunk in _orchestrator.ExecuteAsync(
            workId, firstUserMessage, cancellationToken))
        {
            yield return chunk;
        }

        if (!string.IsNullOrWhiteSpace(workId))
            await _snapshotService.CaptureAfterSnapshotAsync(workId, correlationId);

        if (sessionId != null)
            await _sessionManager.RecordTurnAsync(sessionId);
    }
}
