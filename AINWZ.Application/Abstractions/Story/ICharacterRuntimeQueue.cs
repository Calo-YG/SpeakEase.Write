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
    public CharacterStateChangeProposal Proposal { get; init; }
}

public interface ICharacterRuntimeProcessor
{
    Task ProcessAsync(
        CharacterStateRefreshRequest request,
        CancellationToken cancellationToken = default);
}
