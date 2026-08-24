using SpeakEase.AI.Lib.Models;

namespace SpeakEase.Write.Application.Abstractions.AI;

public interface IAgentRunStore
{
    Task<AgentRunStartResult> StartAsync(
        string workId,
        string sessionId,
        string deduplicationKey,
        string clientMessageId,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        string runId,
        AgentResponse response,
        CancellationToken cancellationToken = default);

    Task AppendEventAsync(
        string runId,
        string stepId,
        long sequence,
        string type,
        object payload,
        CancellationToken cancellationToken = default);

    Task SaveArtifactAsync(
        string runId,
        string stepId,
        string contentType,
        string summary,
        string content,
        int estimatedTokens,
        CancellationToken cancellationToken = default);

    Task RecordToolCallAsync(
        string runId,
        string stepId,
        SpeakEase.AI.Lib.OpenAIModel.ToolCall toolCall,
        CancellationToken cancellationToken = default);
}

public sealed class AgentRunStartResult
{
    public string RunId { get; init; } = string.Empty;
    public bool IsReplay { get; init; }
    public bool IsInProgress { get; init; }
    public AgentResponse ExistingResponse { get; init; }
}
