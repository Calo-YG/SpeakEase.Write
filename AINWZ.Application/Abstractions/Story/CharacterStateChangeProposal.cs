namespace SpeakEase.Write.Application.Abstractions.Story;

public sealed class CharacterStateChangeProposal
{
    public string WorkId { get; init; } = string.Empty;
    public string CharacterId { get; init; } = string.Empty;
    public string SourceRunId { get; init; } = string.Empty;
    public string SourceChapterId { get; init; } = string.Empty;
    public string SourceArtifactId { get; init; } = string.Empty;
    public string SourceEventKey { get; init; } = string.Empty;
    public IReadOnlyList<CharacterStateEvidence> Evidence { get; init; } = Array.Empty<CharacterStateEvidence>();
    public IReadOnlyList<CharacterStateChange> Changes { get; init; } = Array.Empty<CharacterStateChange>();
    public double Confidence { get; init; }
    public long Version { get; init; }
}

public sealed class CharacterStateEvidence
{
    public string Quote { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}

public sealed class CharacterStateChange
{
    public string Dimension { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
}

public sealed class CharacterStateEvaluationResult
{
    public string Status { get; init; } = string.Empty;
    public string Severity { get; init; } = "normal";
    public string Reason { get; init; } = string.Empty;
    public CharacterStateChangeProposal Proposal { get; init; }
}
