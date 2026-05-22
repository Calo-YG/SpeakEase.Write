namespace SpeakEase.Write.Infrastructure.AI.Memory;

public interface IMemoryProvider
{
    Task<SessionMemorySnapshot> LoadSessionMemoryAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken = default);

    Task RefreshAfterTurnAsync(
        string userId,
        string workId,
        string sessionId,
        int turnNumber,
        CancellationToken cancellationToken = default);

    Task InvalidateSessionAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken = default);

    Task LoadAsync(string userId, string workId, CancellationToken cancellationToken = default);
    Task SaveSnapshotAsync(string userId, string workId, CancellationToken cancellationToken = default);
    Task InvalidateAsync(string userId, string workId, CancellationToken cancellationToken = default);
}

public sealed class SessionMemorySnapshot
{
    public static SessionMemorySnapshot Empty => new();

    public string SnapshotId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string SnapshotJson { get; set; } = string.Empty;

    public int TurnNumber { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool HasSnapshot => !string.IsNullOrWhiteSpace(SnapshotId);
}
