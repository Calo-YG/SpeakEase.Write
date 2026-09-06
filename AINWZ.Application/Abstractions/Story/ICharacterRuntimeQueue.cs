namespace SpeakEase.Write.Application.Abstractions.Story;

public interface ICharacterRuntimeQueue
{
    ValueTask EnqueueAsync(
        CharacterStateRefreshRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CharacterStateRefreshRequest
{
    public string UserId { get; init; } = string.Empty;
    public string WorkId { get; init; } = string.Empty;
    public string SourceRunId { get; init; } = string.Empty;
    public string SourceChapterId { get; init; } = string.Empty;
    public string SourceArtifactId { get; init; } = string.Empty;
    public string ChapterContent { get; init; } = string.Empty;
    public CharacterStateChangeProposal Proposal { get; init; }
}

public interface ICharacterStateProposalExtractor
{
    Task<IReadOnlyList<CharacterStateChangeProposal>> ExtractAsync(
        CharacterStateRefreshRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICharacterRuntimeProcessor
{
    Task ProcessAsync(
        CharacterStateRefreshRequest request,
        CancellationToken cancellationToken = default);
}
