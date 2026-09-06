using SpeakEase.Write.Application.Abstractions.Story;

namespace SpeakEase.Write.Infrastructure.AI.Character;

public sealed class GrowthConsistencyValidator : IGrowthConsistencyValidator
{
    private static readonly HashSet<string> MajorDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "core_value",
        "core_motivation",
        "permanent_personality",
        "relationship.reversal"
    };

    public Task<CharacterStateEvaluationResult> ValidateAsync(
        CharacterStateChangeProposal proposal,
        CharacterStateSnapshotData currentSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        cancellationToken.ThrowIfCancellationRequested();

        if (proposal.Evidence.Count == 0 || proposal.Changes.Count == 0 ||
            proposal.Confidence is < 0 or > 1 ||
            string.IsNullOrWhiteSpace(proposal.SourceRunId))
        {
            return Task.FromResult(Result(proposal, "rejected", "提案证据、置信度或来源无效。"));
        }

        if (currentSnapshot is not null && proposal.Version <= currentSnapshot.Version)
            return Task.FromResult(Result(proposal, "rejected", "提案版本不高于当前已确认状态。"));

        if (currentSnapshot is not null &&
            (!string.Equals(currentSnapshot.WorkId, proposal.WorkId, StringComparison.Ordinal) ||
             !string.Equals(currentSnapshot.CharacterId, proposal.CharacterId, StringComparison.Ordinal)))
        {
            return Task.FromResult(Result(proposal, "rejected", "提案与状态快照不属于同一角色。"));
        }

        var status = proposal.Changes.Any(x => MajorDimensions.Contains(x.Dimension))
            ? "needs_review"
            : "approved";
        return Task.FromResult(Result(proposal, status, string.Empty));
    }

    private static CharacterStateEvaluationResult Result(
        CharacterStateChangeProposal proposal,
        string status,
        string reason)
        => new()
        {
            Proposal = proposal,
            Status = status,
            Severity = status == "needs_review" ? "major" : "normal",
            Reason = reason
        };
}
