namespace SpeakEase.Write.Application.Abstractions.Story;

public sealed class PlotHookProposal
{
    public string CharacterId { get; init; } = string.Empty;
    public long StateVersion { get; init; }
    public string SourceDimension { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double Confidence { get; init; }
}
