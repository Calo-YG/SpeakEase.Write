using System.Runtime.CompilerServices;

using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Story;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.AI.Character;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace AINWZ.Tests.AI;

public sealed class CharacterStateProposalExtractorTests
{
    [Fact]
    public async Task ExtractAsync_MapsEvidenceAndChangesToKnownCharacter()
    {
        await using var db = TestDb.Create();
        db.Characters.Add(new CharacterEntity
        {
            Id = "char-1",
            WorkId = "work-1",
            OwnerId = "user-1",
            Name = "林舟",
            CreateBy = "user-1",
            UpdateBy = "user-1"
        });
        await db.SaveChangesAsync();
        var extractor = new CharacterStateProposalExtractor(
            new JsonExtractionLlm(),
            new StaticOpenAIContext(),
            db);

        var proposals = await extractor.ExtractAsync(new CharacterStateRefreshRequest
        {
            UserId = "user-1",
            WorkId = "work-1",
            SourceRunId = "run-1",
            SourceChapterId = "chapter-1",
            SourceArtifactId = "run-1:step-1",
            ChapterContent = "林舟终于放下恐惧，推门走了进去。"
        });

        var proposal = Assert.Single(proposals);
        Assert.Equal("char-1", proposal.CharacterId);
        Assert.Equal("run-1", proposal.SourceRunId);
        Assert.Equal("chapter-1", proposal.SourceChapterId);
        Assert.Equal("emotion.fear", Assert.Single(proposal.Changes).Dimension);
        Assert.Contains("推门", Assert.Single(proposal.Evidence).Quote);
    }

    private sealed class JsonExtractionLlm : IChatCompatible
    {
        public Task<LLMTurnResult> ChatAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new LLMTurnResult
            {
                Success = true,
                Model = context.Model,
                Content = "[{\"characterId\":\"char-1\",\"evidence\":[{\"quote\":\"推门走了进去\",\"type\":\"action\"}],\"changes\":[{\"dimension\":\"emotion.fear\",\"from\":\"fearful\",\"to\":\"resolved\"}],\"confidence\":0.9}]"
            });

        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class StaticOpenAIContext : IOpenAIContext
    {
        public string ApiKey => string.Empty;
        public string Url => string.Empty;
        public string Model => "test";
        public int MaxTokens => 512;
        public int MaxOutputTokens => 512;
        public int ContextWindow => 8_000;
        public Task ResolveAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task InvalidateAsync(string userId = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}
