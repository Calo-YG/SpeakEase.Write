using Microsoft.EntityFrameworkCore;

using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Application.Abstractions.Memory;
using SpeakEase.Write.Application.Abstractions.Persistence;
using ApplicationMemoryProvider = SpeakEase.Write.Application.Abstractions.AI.IMemoryProvider;

namespace SpeakEase.Write.Infrastructure.AI.Memory;

public sealed class MemoryContextProvider(
    ApplicationMemoryProvider memoryProvider,
    IAgentRuntimeDbContext runtimeDb) : IMemoryContextProvider
{
    private readonly ApplicationMemoryProvider _memoryProvider = memoryProvider ?? throw new ArgumentNullException(nameof(memoryProvider));
    private readonly IAgentRuntimeDbContext _runtimeDb = runtimeDb ?? throw new ArgumentNullException(nameof(runtimeDb));

    public async Task<MemoryContextLayers> LoadAsync(
        MemoryContextRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _memoryProvider.LoadSessionMemoryAsync(
            request.UserId, request.WorkId, request.SessionId, cancellationToken);
        var facts = await _memoryProvider.LoadProjectFactsAsync(
            request.UserId, request.WorkId, cancellationToken);

        var recentRunIds = await _runtimeDb.AgentRuns
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId && x.WorkId == request.WorkId && x.Status == "completed")
            .OrderByDescending(x => x.StartedAt)
            .Select(x => x.Id)
            .Take(12)
            .ToListAsync(cancellationToken);
        var take = Math.Clamp(request.MaxRetrievedArtifacts, 0, 16);
        var artifacts = take == 0 || recentRunIds.Count == 0
            ? new List<RetrievedMemoryArtifact>()
            : await _runtimeDb.AgentArtifacts
                .AsNoTracking()
                .Where(x => recentRunIds.Contains(x.RunId))
                .OrderByDescending(x => x.UpdateAt)
                .Take(take)
                .Select(x => new RetrievedMemoryArtifact
                {
                    ArtifactId = x.Id,
                    ContentType = x.ContentType,
                    Summary = x.Summary
                })
                .ToListAsync(cancellationToken);

        return new MemoryContextLayers
        {
            Session = session,
            ProjectFacts = facts,
            RetrievedArtifacts = artifacts
        };
    }
}
