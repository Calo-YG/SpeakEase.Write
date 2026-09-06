using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using SpeakEase.Write.Application.Abstractions.Story;

namespace SpeakEase.Write.Infrastructure.AI.Character;

public sealed class CharacterRuntimeProcessor(
    ICharacterStateEvaluator evaluator,
    IGrowthConsistencyValidator validator,
    ICharacterStateStore store,
    IPlotHookGenerator plotHookGenerator,
    ICharacterStateProposalExtractor extractor = null) : ICharacterRuntimeProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ICharacterStateEvaluator _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    private readonly IGrowthConsistencyValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly ICharacterStateStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IPlotHookGenerator _plotHookGenerator = plotHookGenerator ?? throw new ArgumentNullException(nameof(plotHookGenerator));
    private readonly ICharacterStateProposalExtractor _extractor = extractor;

    public async Task ProcessAsync(
        CharacterStateRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new ArgumentException("Character refresh requires a user id.", nameof(request));

        if (request.Proposal is null)
        {
            if (_extractor is null || string.IsNullOrWhiteSpace(request.ChapterContent))
                return;

            var extracted = await _extractor.ExtractAsync(request, cancellationToken);
            foreach (var proposal in extracted)
                await ProcessProposalAsync(request.UserId, proposal, cancellationToken);
            return;
        }

        await ProcessProposalAsync(request.UserId, request.Proposal, cancellationToken);
    }

    private async Task ProcessProposalAsync(
        string userId,
        CharacterStateChangeProposal proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var current = await _store.EnsureBaselineAsync(
            userId,
            proposal.WorkId,
            proposal.CharacterId,
            cancellationToken);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var normalized = NormalizeVersion(proposal, (current?.Version ?? -1) + 1);
            var evaluation = await _evaluator.EvaluateAsync(normalized, cancellationToken);
            if (evaluation.Status == "rejected")
                return;

            var validation = await _validator.ValidateAsync(normalized, current, cancellationToken);
            if (validation.Status == "rejected")
                return;

            if (evaluation.Status == "needs_review" || validation.Status == "needs_review")
            {
                await _store.SaveGrowthProposalAsync(new CharacterGrowthProposalData
                {
                    UserId = userId,
                    WorkId = normalized.WorkId,
                    CharacterId = normalized.CharacterId,
                    SourceRunId = normalized.SourceRunId,
                    ProposalJson = JsonSerializer.Serialize(normalized, JsonOptions),
                    Severity = "major",
                    Status = "needs_review"
                }, cancellationToken);
                return;
            }

            var snapshot = new CharacterStateSnapshotData
            {
                UserId = userId,
                WorkId = normalized.WorkId,
                CharacterId = normalized.CharacterId,
                StateJson = ApplyChanges(current?.StateJson, normalized.Changes),
                Version = normalized.Version,
                Status = "confirmed"
            };
            var hooks = await _plotHookGenerator.GenerateAsync(snapshot, cancellationToken);
            snapshot = WithPlotHooks(snapshot, hooks);
            var committed = await _store.TryCommitStateChangeAsync(new CharacterStateEventData
            {
                UserId = userId,
                WorkId = normalized.WorkId,
                CharacterId = normalized.CharacterId,
                SourceRunId = normalized.SourceRunId,
                SourceChapterId = normalized.SourceChapterId,
                SourceEventKey = ResolveSourceEventKey(normalized),
                EventType = "character_state_changed",
                EvidenceJson = JsonSerializer.Serialize(normalized.Evidence, JsonOptions),
                ChangesJson = JsonSerializer.Serialize(normalized.Changes, JsonOptions),
                Confidence = normalized.Confidence,
                Version = normalized.Version
            }, snapshot, current?.Version ?? -1, cancellationToken);
            if (committed)
                return;

            current = await _store.GetLatestSnapshotAsync(
                userId,
                normalized.WorkId,
                normalized.CharacterId,
                cancellationToken);
        }

        throw new InvalidOperationException("Character state update exceeded concurrency retry limit.");
    }

    private static CharacterStateChangeProposal NormalizeVersion(CharacterStateChangeProposal proposal, long nextVersion)
        => new()
        {
            WorkId = proposal.WorkId,
            CharacterId = proposal.CharacterId,
            SourceRunId = proposal.SourceRunId,
            SourceChapterId = proposal.SourceChapterId,
            SourceArtifactId = proposal.SourceArtifactId,
            SourceEventKey = proposal.SourceEventKey,
            Evidence = proposal.Evidence,
            Changes = proposal.Changes,
            Confidence = proposal.Confidence,
            Version = Math.Max(proposal.Version, nextVersion)
        };

    private static CharacterStateSnapshotData WithPlotHooks(
        CharacterStateSnapshotData snapshot,
        IReadOnlyList<PlotHookProposal> hooks)
    {
        if (hooks.Count == 0)
            return snapshot;

        var state = JsonNode.Parse(snapshot.StateJson) as JsonObject ?? new JsonObject();
        state["plotHooks"] = JsonSerializer.SerializeToNode(hooks, JsonOptions);
        return new CharacterStateSnapshotData
        {
            UserId = snapshot.UserId,
            Id = snapshot.Id,
            WorkId = snapshot.WorkId,
            CharacterId = snapshot.CharacterId,
            BasedOnEventId = snapshot.BasedOnEventId,
            StateJson = state.ToJsonString(JsonOptions),
            Version = snapshot.Version,
            Status = snapshot.Status
        };
    }

    private static string ResolveSourceEventKey(CharacterStateChangeProposal proposal)
    {
        if (!string.IsNullOrWhiteSpace(proposal.SourceEventKey))
            return proposal.SourceEventKey;
        if (!string.IsNullOrWhiteSpace(proposal.SourceArtifactId))
            return proposal.SourceArtifactId;
        if (!string.IsNullOrWhiteSpace(proposal.SourceChapterId))
            return proposal.SourceChapterId;
        return proposal.SourceRunId;
    }

    private static string ApplyChanges(string stateJson, IReadOnlyList<CharacterStateChange> changes)
    {
        JsonObject state;
        try
        {
            state = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            state = new JsonObject();
        }

        foreach (var change in changes)
        {
            if (!string.IsNullOrWhiteSpace(change.Dimension))
                state[change.Dimension] = change.To;
        }

        return state.ToJsonString(JsonOptions);
    }
}
