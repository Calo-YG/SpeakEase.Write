namespace SpeakEase.Write.Application.Abstractions.Story;

public interface IPlotHookGenerator
{
    Task<IReadOnlyList<PlotHookProposal>> GenerateAsync(
        CharacterStateSnapshotData snapshot,
        CancellationToken cancellationToken = default);
}
