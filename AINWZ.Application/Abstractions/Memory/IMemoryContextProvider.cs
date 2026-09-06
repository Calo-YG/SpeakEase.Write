using SpeakEase.Write.Application.Abstractions.AI;

namespace SpeakEase.Write.Application.Abstractions.Memory;

public interface IMemoryContextProvider
{
    Task<MemoryContextLayers> LoadAsync(
        MemoryContextRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class MemoryContextRequest
{
    public string UserId { get; init; } = string.Empty;
    public string WorkId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
    public int MaxRetrievedArtifacts { get; init; } = 8;
}

public sealed class MemoryContextLayers
{
    public SessionMemorySnapshot Session { get; init; } = SessionMemorySnapshot.Empty;
    public IReadOnlyList<MemoryFact> ProjectFacts { get; init; } = Array.Empty<MemoryFact>();
    public IReadOnlyList<RetrievedMemoryArtifact> RetrievedArtifacts { get; init; } = Array.Empty<RetrievedMemoryArtifact>();
}

public sealed class RetrievedMemoryArtifact
{
    public string ArtifactId { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}
