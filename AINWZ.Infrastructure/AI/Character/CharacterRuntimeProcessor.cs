using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using SpeakEase.Write.Application.Abstractions.Story;

namespace SpeakEase.Write.Infrastructure.AI.Character;

public sealed class CharacterRuntimeProcessor(
    ICharacterStateEvaluator evaluator,
    IGrowthConsistencyValidator validator,
    ICharacterStateStore store,
    IPlotHookGenerator plotHookGenerator) : ICharacterRuntimeProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ICharacterStateEvaluator _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    private readonly IGrowthConsistencyValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly ICharacterStateStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IPlotHookGenerator _plotHookGenerator = plotHookGenerator ?? throw new ArgumentNullException(nameof(plotHookGenerator));

    public async Task ProcessAsync(
        CharacterStateRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Proposal);
        if (string.IsNullOrWhiteSpace(request.UserId))
            throw new ArgumentException("Character refresh requires a user id.", nameof(request));

        var proposal = request.Proposal;
        var current = await _store.EnsureBaselineAsync(
            request.UserId,
            proposal.WorkId,
            proposal.CharacterId,
            cancellationToken);
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
                UserId = request.UserId,
                WorkId = normalized.WorkId,
                CharacterId = normalized.CharacterId,
                SourceRunId = normalized.SourceRunId,
                ProposalJson = JsonSerializer.Serialize(normalized, JsonOptions),
                Severity = "major",
                Status = "needs_review"
            }, cancellationToken);
            return;
        }

        var eventId = await _store.AppendEventAsync(new CharacterStateEventData
        {
            UserId = request.UserId,
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
        }, cancellationToken);

        var snapshot = new CharacterStateSnapshotData
        {
            UserId = request.UserId,
            WorkId = normalized.WorkId,
            CharacterId = normalized.CharacterId,
            BasedOnEventId = eventId,
            StateJson = ApplyChanges(current?.StateJson, normalized.Changes),
            Version = normalized.Version,
            Status = "confirmed"
        };
        await _store.SaveSnapshotAsync(snapshot, cancellationToken);

        // 预计算可拓展剧情候选，当前只作为 Runtime 派生结果；Task 9 再交给 PlanCompiler 选用。
        await _plotHookGenerator.GenerateAsync(snapshot, cancellationToken);
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
            Version = proposal.Version > 0 ? proposal.Version : nextVersion
        };

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
