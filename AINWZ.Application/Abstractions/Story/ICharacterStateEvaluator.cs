namespace SpeakEase.Write.Application.Abstractions.Story;

public interface ICharacterStateEvaluator
{
    Task<CharacterStateEvaluationResult> EvaluateAsync(
        CharacterStateChangeProposal proposal,
        CancellationToken cancellationToken = default);
}
