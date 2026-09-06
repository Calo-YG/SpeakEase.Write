using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace SpeakEase.Write.Infrastructure.AI.Character;

public sealed class CharacterStateProposalExtractor(
    IChatCompatible llm,
    IOpenAIContext llmContext,
    ICharacterDbContext db) : ICharacterStateProposalExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IChatCompatible _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    private readonly IOpenAIContext _llmContext = llmContext ?? throw new ArgumentNullException(nameof(llmContext));
    private readonly ICharacterDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<CharacterStateChangeProposal>> ExtractAsync(
        CharacterStateRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.WorkId) ||
            string.IsNullOrWhiteSpace(request.SourceRunId) ||
            string.IsNullOrWhiteSpace(request.ChapterContent))
        {
            return Array.Empty<CharacterStateChangeProposal>();
        }

        var characters = await _db.Characters.AsNoTracking()
            .Where(x => x.OwnerId == request.UserId && x.WorkId == request.WorkId)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .Take(128)
            .ToListAsync(cancellationToken);
        if (characters.Count == 0)
            return Array.Empty<CharacterStateChangeProposal>();

        await _llmContext.ResolveAsync(cancellationToken);
        var content = request.ChapterContent.Length > 32_000
            ? request.ChapterContent[..32_000]
            : request.ChapterContent;
        var result = await _llm.ChatAsync(
            new LLMTurnContext
            {
                Model = _llmContext.Model,
                Temperature = 0.1,
                MaxTokens = Math.Min(2_048, Math.Max(256, _llmContext.MaxOutputTokens))
            },
            new List<ChatMessage>
            {
                ChatMessage.System(
                    "Extract only durable character-state changes explicitly evidenced by the chapter. " +
                    "Return a JSON array. Each item: characterId, evidence[{quote,type}], " +
                    "changes[{dimension,from,to}], confidence. Omit unchanged characters and never invent evidence."),
                ChatMessage.User($"Known characters:\n{JsonSerializer.Serialize(characters)}\n\nChapter:\n{content}")
            },
            Array.Empty<ToolDefinition>(),
            cancellationToken);
        if (result?.Success != true || string.IsNullOrWhiteSpace(result.Content))
            return Array.Empty<CharacterStateChangeProposal>();

        Candidate[] candidates;
        try
        {
            candidates = JsonSerializer.Deserialize<Candidate[]>(UnwrapJson(result.Content), JsonOptions)
                         ?? Array.Empty<Candidate>();
        }
        catch (JsonException)
        {
            return Array.Empty<CharacterStateChangeProposal>();
        }

        var knownIds = characters.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        return candidates
            .Where(candidate => knownIds.Contains(candidate.CharacterId ?? string.Empty))
            .Select(candidate => ToProposal(candidate, request, content))
            .Where(proposal => proposal.Evidence.Count > 0 && proposal.Changes.Count > 0)
            .ToArray();
    }

    private static CharacterStateChangeProposal ToProposal(
        Candidate candidate,
        CharacterStateRefreshRequest request,
        string chapterContent)
    {
        var evidence = (candidate.Evidence ?? Array.Empty<CandidateEvidence>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Quote) &&
                        chapterContent.Contains(x.Quote, StringComparison.Ordinal))
            .Select(x => new CharacterStateEvidence
            {
                Quote = x.Quote,
                Type = x.Type ?? string.Empty
            })
            .ToArray();
        var changes = (candidate.Changes ?? Array.Empty<CandidateChange>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Dimension) && !string.IsNullOrWhiteSpace(x.To))
            .Select(x => new CharacterStateChange
            {
                Dimension = x.Dimension,
                From = x.From ?? string.Empty,
                To = x.To
            })
            .ToArray();
        return new CharacterStateChangeProposal
        {
            WorkId = request.WorkId,
            CharacterId = candidate.CharacterId ?? string.Empty,
            SourceRunId = request.SourceRunId,
            SourceChapterId = request.SourceChapterId,
            SourceArtifactId = request.SourceArtifactId,
            SourceEventKey = $"{request.SourceArtifactId}:{candidate.CharacterId}",
            Evidence = evidence,
            Changes = changes,
            Confidence = Math.Clamp(candidate.Confidence, 0, 1)
        };
    }

    private static string UnwrapJson(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstLine >= 0 && lastFence > firstLine
            ? trimmed[(firstLine + 1)..lastFence].Trim()
            : trimmed;
    }

    private sealed class Candidate
    {
        public string CharacterId { get; init; } = string.Empty;
        public IReadOnlyList<CandidateEvidence> Evidence { get; init; } = Array.Empty<CandidateEvidence>();
        public IReadOnlyList<CandidateChange> Changes { get; init; } = Array.Empty<CandidateChange>();
        public double Confidence { get; init; }
    }

    private sealed class CandidateEvidence
    {
        public string Quote { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
    }

    private sealed class CandidateChange
    {
        public string Dimension { get; init; } = string.Empty;
        public string From { get; init; } = string.Empty;
        public string To { get; init; } = string.Empty;
    }
}
