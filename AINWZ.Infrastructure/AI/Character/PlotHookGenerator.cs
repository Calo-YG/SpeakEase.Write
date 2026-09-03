using System.Text.Json;
using SpeakEase.Write.Application.Abstractions.Story;

namespace SpeakEase.Write.Infrastructure.AI.Character;

public sealed class PlotHookGenerator : IPlotHookGenerator
{
    public Task<IReadOnlyList<PlotHookProposal>> GenerateAsync(
        CharacterStateSnapshotData snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var hooks = new List<PlotHookProposal>();
        try
        {
            using var document = JsonDocument.Parse(snapshot.StateJson);
            AddHooks(document.RootElement, "goals", "推动未满足目标：", 0.8, snapshot, hooks);
            AddHooks(document.RootElement, "conflicts", "放大内部冲突：", 0.75, snapshot, hooks);
        }
        catch (JsonException)
        {
            return Task.FromResult<IReadOnlyList<PlotHookProposal>>(hooks);
        }

        return Task.FromResult<IReadOnlyList<PlotHookProposal>>(hooks);
    }

    private static void AddHooks(
        JsonElement root,
        string propertyName,
        string prefix,
        double confidence,
        CharacterStateSnapshotData snapshot,
        List<PlotHookProposal> hooks)
    {
        if (!root.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
            return;

        foreach (var value in values.EnumerateArray().Take(3))
        {
            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            hooks.Add(new PlotHookProposal
            {
                CharacterId = snapshot.CharacterId,
                StateVersion = snapshot.Version,
                SourceDimension = propertyName,
                Description = prefix + text,
                Confidence = confidence
            });
        }
    }
}
