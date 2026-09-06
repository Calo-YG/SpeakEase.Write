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
        store.Setup(x => x.TryCommitStateChangeAsync(
                It.IsAny<CharacterStateEventData>(),
                It.IsAny<CharacterStateSnapshotData>(),
                2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var processor = CreateProcessor(store.Object);

        await processor.ProcessAsync(new CharacterStateRefreshRequest
        {
            UserId = "user-1",
            Proposal = Proposal("emotion.fear")
        });

        store.Verify(x => x.TryCommitStateChangeAsync(
            It.Is<CharacterStateEventData>(e => e.Version == 3 && e.UserId == "user-1"),
            It.Is<CharacterStateSnapshotData>(s => s.Version == 3),
            2,
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
        store.Verify(x => x.TryCommitStateChangeAsync(
            It.IsAny<CharacterStateEventData>(),
            It.IsAny<CharacterStateSnapshotData>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentSnapshotAdvance_RebasesAndRetriesWithoutDroppingChange()
    {
        var store = new RebaseStore();
        var processor = CreateProcessor(store);

        await processor.ProcessAsync(new CharacterStateRefreshRequest
        {
            UserId = "user-1",
            Proposal = Proposal("emotion.fear")
        });

        Assert.Equal(2, store.CommitAttempts);
        Assert.Equal(4, store.CommittedSnapshot.Version);
        Assert.Contains("other", store.CommittedSnapshot.StateJson);
        Assert.Contains("emotion.fear", store.CommittedSnapshot.StateJson);
    }

    [Fact]
    public async Task ProcessAsync_ChapterArtifact_ExtractsAndCommitsCharacterChanges()
    {
        var store = new RebaseStore { FailFirstCommit = false };
        var extractor = new StaticProposalExtractor(Proposal("emotion.resolve"));
        var processor = new CharacterRuntimeProcessor(
            new CharacterStateEvaluator(),
            new GrowthConsistencyValidator(),
            store,
            new PlotHookGenerator(),
            extractor);

        await processor.ProcessAsync(new CharacterStateRefreshRequest
        {
            UserId = "user-1",
            WorkId = "work-1",
            SourceRunId = "run-1",
            SourceChapterId = "chapter-1",
            SourceArtifactId = "run-1:step-1",
            ChapterContent = "角色做出了选择。"
        });

        Assert.Equal(1, extractor.Calls);
        Assert.Equal(1, store.CommitAttempts);
        Assert.Equal(3, store.CommittedSnapshot.Version);
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

    private sealed class RebaseStore : ICharacterStateStore
    {
        private CharacterStateSnapshotData _current = new()
        {
            UserId = "user-1",
            WorkId = "work-1",
            CharacterId = "char-1",
            Version = 2,
            StateJson = "{}"
        };

        public int CommitAttempts { get; private set; }
        public CharacterStateSnapshotData CommittedSnapshot { get; private set; }
        public bool FailFirstCommit { get; init; } = true;

        public Task<CharacterStateSnapshotData> EnsureBaselineAsync(string workId, string characterId, CancellationToken cancellationToken = default)
            => Task.FromResult(_current);
        public Task<CharacterStateSnapshotData> EnsureBaselineAsync(string userId, string workId, string characterId, CancellationToken cancellationToken = default)
            => Task.FromResult(_current);
        public Task<CharacterStateSnapshotData> GetLatestSnapshotAsync(string workId, string characterId, CancellationToken cancellationToken = default)
            => Task.FromResult(_current);
        public Task<CharacterStateSnapshotData> GetLatestSnapshotAsync(string userId, string workId, string characterId, CancellationToken cancellationToken = default)
            => Task.FromResult(_current);
        public Task<IReadOnlyList<CharacterStateSnapshotData>> GetWorkSnapshotsAsync(string userId, string workId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CharacterStateSnapshotData>>(new[] { _current });
        public Task SaveSnapshotAsync(CharacterStateSnapshotData snapshot, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<string> AppendEventAsync(CharacterStateEventData stateEvent, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task SaveGrowthProposalAsync(CharacterGrowthProposalData proposal, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> TryCommitStateChangeAsync(
            CharacterStateEventData stateEvent,
            CharacterStateSnapshotData snapshot,
            long expectedVersion,
            CancellationToken cancellationToken = default)
        {
            CommitAttempts++;
            if (FailFirstCommit && CommitAttempts == 1)
            {
                _current = new CharacterStateSnapshotData
                {
                    UserId = "user-1",
                    WorkId = "work-1",
                    CharacterId = "char-1",
                    Version = 3,
                    StateJson = "{\"other\":\"preserved\"}"
                };
                return Task.FromResult(false);
            }

            CommittedSnapshot = snapshot;
            _current = snapshot;
            return Task.FromResult(true);
        }
    }

    private sealed class StaticProposalExtractor(CharacterStateChangeProposal proposal)
        : ICharacterStateProposalExtractor
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<CharacterStateChangeProposal>> ExtractAsync(
            CharacterStateRefreshRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<CharacterStateChangeProposal>>(new[] { proposal });
        }
    }
}
