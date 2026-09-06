namespace SpeakEase.Write.Application.Abstractions.Story;

public interface IGrowthConsistencyValidator
{
    Task<CharacterStateEvaluationResult> ValidateAsync(
        CharacterStateChangeProposal proposal,
        CharacterStateSnapshotData currentSnapshot,
        CancellationToken cancellationToken = default);
}
