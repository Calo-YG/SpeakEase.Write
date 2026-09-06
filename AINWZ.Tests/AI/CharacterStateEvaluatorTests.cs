using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Infrastructure.AI.Character;

namespace AINWZ.Tests.AI;

public sealed class CharacterStateEvaluatorTests
{
    [Fact]
    public async Task Evaluate_NormalEmotionChange_IsAutoApproved()
    {
        var evaluator = new CharacterStateEvaluator();
        var result = await evaluator.EvaluateAsync(new CharacterStateChangeProposal
        {
            WorkId = "work-1",
            CharacterId = "char-1",
            SourceRunId = "run-1",
            Evidence = new[] { new CharacterStateEvidence { Quote = "他感到紧张", Type = "emotion" } },
            Changes = new[] { new CharacterStateChange { Dimension = "emotion.fear", From = "0.2", To = "0.6" } },
            Confidence = 0.9
        });

        Assert.Equal("approved", result.Status);
    }

    [Fact]
    public async Task Evaluate_CoreValueChange_RequiresReview()
    {
        var evaluator = new CharacterStateEvaluator();
        var result = await evaluator.EvaluateAsync(new CharacterStateChangeProposal
        {
            WorkId = "work-1",
            CharacterId = "char-1",
            SourceRunId = "run-1",
            Evidence = new[] { new CharacterStateEvidence { Quote = "他决定背弃誓言", Type = "decision" } },
            Changes = new[] { new CharacterStateChange { Dimension = "core_value", From = "守信", To = "只看结果" } },
            Confidence = 0.9
        });

        Assert.Equal("needs_review", result.Status);
    }

    [Fact]
    public async Task Evaluate_WithoutEvidence_IsRejected()
    {
        var evaluator = new CharacterStateEvaluator();
        var result = await evaluator.EvaluateAsync(new CharacterStateChangeProposal
        {
            WorkId = "work-1", CharacterId = "char-1", SourceRunId = "run-1",
            Changes = new[] { new CharacterStateChange { Dimension = "emotion.fear", To = "0.6" } }
        });

        Assert.Equal("rejected", result.Status);
    }
}
