using SpeakEase.Write.Application.Abstractions.Story;

namespace SpeakEase.Write.Infrastructure.AI.Character;

public sealed class CharacterStateEvaluator : ICharacterStateEvaluator
{
    private static readonly HashSet<string> MajorDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "core_value",
        "core_motivation",
        "permanent_personality",
        "relationship.reversal"
    };

    public Task<CharacterStateEvaluationResult> EvaluateAsync(
        CharacterStateChangeProposal proposal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(proposal.WorkId) ||
            string.IsNullOrWhiteSpace(proposal.CharacterId) ||
            string.IsNullOrWhiteSpace(proposal.SourceRunId) ||
            proposal.Evidence.Count == 0 ||
            proposal.Evidence.All(x => string.IsNullOrWhiteSpace(x.Quote)) ||
            proposal.Changes.Count == 0)
        {
            return Task.FromResult(Result(proposal, "rejected", "normal", "状态变化缺少运行来源、证据或变更内容。"));
        }

        var requiresReview = proposal.Changes.Any(x => MajorDimensions.Contains(x.Dimension));
        return Task.FromResult(requiresReview
            ? Result(proposal, "needs_review", "major", "核心价值观、动机、永久人格或重大关系反转需要审核。")
            : Result(proposal, "approved", "normal", "具备可追溯证据的普通动态变化可自动提交。"));
    }

    private static CharacterStateEvaluationResult Result(
        CharacterStateChangeProposal proposal,
        string status,
        string severity,
        string reason)
        => new()
        {
            Proposal = proposal,
            Status = status,
            Severity = severity,
            Reason = reason
        };
}
