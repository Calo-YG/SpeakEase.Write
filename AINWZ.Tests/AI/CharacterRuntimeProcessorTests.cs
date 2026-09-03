using Moq;

using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Infrastructure.AI.Character;

namespace AINWZ.Tests.AI;

public sealed class CharacterRuntimeProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ApprovedChange_AppendsEventAndAdvancesSnapshot()
    {
        var store = new Mock<ICharacterStateStore>();
        store.Setup(x => x.EnsureBaselineAsync("user-1", "work-1", "char-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharacterStateSnapshotData
            {
                UserId = "user-1", WorkId = "work-1", CharacterId = "char-1", Version = 2, StateJson = "{}"
            });
        store.Setup(x => x.AppendEventAsync(It.IsAny<CharacterStateEventData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("event-3");
        var processor = CreateProcessor(store.Object);

        await processor.ProcessAsync(new CharacterStateRefreshRequest
        {
            UserId = "user-1",
            Proposal = Proposal("emotion.fear")
        });

        store.Verify(x => x.AppendEventAsync(
            It.Is<CharacterStateEventData>(e => e.Version == 3 && e.UserId == "user-1"),
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.SaveSnapshotAsync(
            It.Is<CharacterStateSnapshotData>(s => s.Version == 3 && s.BasedOnEventId == "event-3"),
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.SaveGrowthProposalAsync(
            It.IsAny<CharacterGrowthProposalData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_MajorChange_SavesProposalWithoutChangingSnapshot()
    {
        var store = new Mock<ICharacterStateStore>();
        store.Setup(x => x.EnsureBaselineAsync("user-1", "work-1", "char-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharacterStateSnapshotData
            {
                UserId = "user-1", WorkId = "work-1", CharacterId = "char-1", Version = 2, StateJson = "{}"
            });
        var processor = CreateProcessor(store.Object);

        await processor.ProcessAsync(new CharacterStateRefreshRequest
        {
            UserId = "user-1",
            Proposal = Proposal("core_value")
        });

        store.Verify(x => x.SaveGrowthProposalAsync(
            It.Is<CharacterGrowthProposalData>(p => p.Status == "needs_review" && p.Severity == "major"),
            It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.AppendEventAsync(
            It.IsAny<CharacterStateEventData>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(x => x.SaveSnapshotAsync(
            It.IsAny<CharacterStateSnapshotData>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CharacterRuntimeProcessor CreateProcessor(ICharacterStateStore store)
        => new(new CharacterStateEvaluator(), new GrowthConsistencyValidator(), store, new PlotHookGenerator());

    private static CharacterStateChangeProposal Proposal(string dimension)
        => new()
        {
            WorkId = "work-1",
            CharacterId = "char-1",
            SourceRunId = "run-1",
            SourceChapterId = "chapter-1",
            Evidence = new[] { new CharacterStateEvidence { Quote = "角色做出了选择", Type = "decision" } },
            Changes = new[] { new CharacterStateChange { Dimension = dimension, From = "before", To = "after" } },
            Confidence = 0.9
        };
}
