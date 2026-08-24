namespace SpeakEase.Write.Application.Abstractions.AI;

public interface IMemoryProvider
{
    Task<SessionMemorySnapshot> LoadSessionMemoryAsync(
        string userId,
        string workId,
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryFact>> LoadProjectFactsAsync(
        string userId,
        string workId,
        CancellationToken cancellationToken = default);

    Task UpsertProjectFactAsync(
        string userId,
        string workId,
        MemoryFact fact,
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
    public int CoveredFromTurn { get; set; }
    public int CoveredToTurn { get; set; }
    public string MemoryStatus { get; set; } = "fresh";
    public DateTime? UpdatedAt { get; set; }
    public bool HasSnapshot => !string.IsNullOrWhiteSpace(SnapshotId);
}

public sealed class MemoryFact
{
    public string Id { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public int SourceTurn { get; init; }
    public double Confidence { get; init; }
    public int VersionTurn { get; init; }
}
