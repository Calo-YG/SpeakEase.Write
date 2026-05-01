using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
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
        var (userMessage, history) = ExtractMessages(request.Messages);

        var correlationId = Guid.NewGuid().ToString();
        var sessionResult = await _sessionManager.GetActiveSessionAsync(workId);
        string sessionId = sessionResult.Data?.SessionId ?? (await _sessionManager.StartSessionAsync(workId)).Data?.SessionId;

        if (!string.IsNullOrWhiteSpace(workId))
            await _snapshotService.CaptureBeforeSnapshotAsync(workId, correlationId);

        var contentParts = new List<string>();

        await foreach (var chunk in _orchestrator.ExecuteAsync(
            workId, userMessage, history, cancellationToken))
        {
            if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                contentParts.Add(chunk.Content);
        }

        if (!string.IsNullOrWhiteSpace(workId))
            await _snapshotService.CaptureAfterSnapshotAsync(workId, correlationId);

        if (sessionId != null)
            await _sessionManager.RecordTurnAsync(sessionId);

        return new AgentResponse
        {
            Content = string.Join(string.Empty, contentParts),
            StopReason = "completed"
        };
    }

    public async IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(
        AgentChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var workId = request.WorkId ?? string.Empty;
        var (userMessage, history) = ExtractMessages(request.Messages);

        var correlationId = Guid.NewGuid().ToString();
        var sessionResult = await _sessionManager.GetActiveSessionAsync(workId);
        string sessionId = sessionResult.Data?.SessionId
                           ?? (await _sessionManager.StartSessionAsync(workId)).Data?.SessionId;

        if (!string.IsNullOrWhiteSpace(workId))
            await _snapshotService.CaptureBeforeSnapshotAsync(workId, correlationId);

        await foreach (var chunk in _orchestrator.ExecuteAsync(
            workId, userMessage, history, cancellationToken))
        {
            yield return chunk;
        }

        if (!string.IsNullOrWhiteSpace(workId))
            await _snapshotService.CaptureAfterSnapshotAsync(workId, correlationId);

        if (sessionId != null)
            await _sessionManager.RecordTurnAsync(sessionId);
    }

    private static (string userMessage, List<ChatMessage> history) ExtractMessages(
        List<AgentChatMessage> messages)
    {
        if (messages == null || messages.Count == 0)
            return (string.Empty, new List<ChatMessage>());

        var lastUserIndex = messages.FindLastIndex(m => m.Role == "user");
        var history = new List<ChatMessage>();
        for (int i = 0; i < lastUserIndex; i++)
        {
            var m = messages[i];
            if (m.Role == "user")
                history.Add(ChatMessage.User(m.Content));
            else if (m.Role == "assistant")
                history.Add(ChatMessage.Assistant(m.Content));
        }

        var lastUserMsg = string.Empty;
        if (lastUserIndex >= 0)
            lastUserMsg = messages[lastUserIndex].Content;

        return (lastUserMsg, history);
    }
}
