using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Infrastructure.AI.Character;

namespace AINWZ.Tests.AI;

public sealed class GrowthConsistencyValidatorTests
{
    [Fact]
    public async Task Validate_RejectsVersionOlderThanSnapshot()
    {
        var validator = new GrowthConsistencyValidator();
        var result = await validator.ValidateAsync(new CharacterStateChangeProposal
        {
            WorkId = "work-1", CharacterId = "char-1", SourceRunId = "run-1", Version = 1,
            Evidence = new[] { new CharacterStateEvidence { Quote = "证据", Type = "decision" } },
            Changes = new[] { new CharacterStateChange { Dimension = "emotion.fear", To = "0.6" } }
        }, new CharacterStateSnapshotData { WorkId = "work-1", CharacterId = "char-1", Version = 2 });

        Assert.Equal("rejected", result.Status);
    }

    [Fact]
    public async Task Validate_RequiresEvidenceAndValidConfidence()
    {
        var validator = new GrowthConsistencyValidator();
        var result = await validator.ValidateAsync(new CharacterStateChangeProposal
        {
            WorkId = "work-1", CharacterId = "char-1", SourceRunId = "run-1", Version = 3,
            Confidence = 1.2,
            Changes = new[] { new CharacterStateChange { Dimension = "emotion.fear", To = "0.6" } }
        }, null);

        Assert.Equal("rejected", result.Status);
    }
}
