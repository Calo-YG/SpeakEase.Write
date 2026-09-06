namespace SpeakEase.AI.Lib.Runtime;

public interface IRuntimeStateStore
{
    Task SaveCheckpointAsync(
        RuntimeCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    Task SaveArtifactAsync(
        RuntimeArtifact artifact,
        CancellationToken cancellationToken = default);
}

public sealed class RuntimeCheckpoint
{
    public string RunId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string MessagesJson { get; init; } = string.Empty;
    public int Iteration { get; init; }
    public string PendingToolCallsJson { get; init; } = string.Empty;
    public long Version { get; init; }
}

public sealed class RuntimeArtifact
{
    public string RunId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public string ContentType { get; init; } = "plain";
    public string Summary { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int EstimatedTokens { get; init; }
}
