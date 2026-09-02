using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpeakEase.Write.Domain.Entities.AI;

namespace SpeakEase.Write.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for durable Agent Runtime execution state.
/// </summary>
public interface IAgentRuntimeDbContext
{
    DbSet<AgentRunEntity> AgentRuns { get; }
    DbSet<AgentRunEventEntity> AgentRunEvents { get; }
    DbSet<AgentToolCallEntity> AgentToolCalls { get; }
    DbSet<AgentArtifactEntity> AgentArtifacts { get; }
    DbSet<AgentCheckpointEntity> AgentCheckpoints { get; }

    DatabaseFacade Database { get; }

    void Detach(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
