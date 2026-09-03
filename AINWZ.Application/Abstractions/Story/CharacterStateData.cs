namespace SpeakEase.Write.Application.Abstractions.Story;

public sealed class CharacterStateEventData
{
    public string WorkId { get; init; } = string.Empty;
    public string CharacterId { get; init; } = string.Empty;
    public string SourceRunId { get; init; } = string.Empty;
    public string SourceChapterId { get; init; } = string.Empty;
    public string SourceEventKey { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string EvidenceJson { get; init; } = string.Empty;
    public string ChangesJson { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public long Version { get; init; }
}

public sealed class CharacterStateSnapshotData
{
    public string Id { get; init; } = string.Empty;
    public string WorkId { get; init; } = string.Empty;
    public string CharacterId { get; init; } = string.Empty;
    public string BasedOnEventId { get; init; } = string.Empty;
    public string StateJson { get; init; } = string.Empty;
    public long Version { get; init; }
    public string Status { get; init; } = "confirmed";
}

public sealed class CharacterGrowthProposalData
{
    public string WorkId { get; init; } = string.Empty;
    public string CharacterId { get; init; } = string.Empty;
    public string SourceRunId { get; init; } = string.Empty;
    public string ProposalJson { get; init; } = string.Empty;
    public string Severity { get; init; } = "normal";
    public string Status { get; init; } = "needs_review";
}

public interface ICharacterStateStore
{
    Task<CharacterStateSnapshotData> EnsureBaselineAsync(
        string workId,
        string characterId,
        CancellationToken cancellationToken = default);

    Task<CharacterStateSnapshotData> GetLatestSnapshotAsync(
        string workId,
        string characterId,
        CancellationToken cancellationToken = default);

    Task SaveSnapshotAsync(
        CharacterStateSnapshotData snapshot,
        CancellationToken cancellationToken = default);

    Task<string> AppendEventAsync(
        CharacterStateEventData stateEvent,
        CancellationToken cancellationToken = default);

    Task SaveGrowthProposalAsync(
        CharacterGrowthProposalData proposal,
        CancellationToken cancellationToken = default);
}
