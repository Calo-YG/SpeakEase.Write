namespace SpeakEase.Write.Application.Abstractions.AI;

public interface IMemoryRefreshQueue
{
    ValueTask EnqueueAsync(
        MemoryRefreshRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class MemoryRefreshRequest
{
    public string UserId { get; init; } = string.Empty;
    public string WorkId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public int TurnNumber { get; init; }
    public string RunId { get; init; } = string.Empty;
}
