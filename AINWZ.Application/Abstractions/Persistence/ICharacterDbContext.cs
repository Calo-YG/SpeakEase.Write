using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpeakEase.Write.Domain.Entities.Story;

namespace SpeakEase.Write.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for characters, relationships, arcs and character graphs.
/// </summary>
public interface ICharacterDbContext
{
    DbSet<CharacterEntity> Characters { get; }
    DbSet<CharacterRelationshipEntity> CharacterRelationships { get; }
    DbSet<CharacterArcEntity> CharacterArcs { get; }
    DbSet<CharacterGraphEntity> CharacterGraphs { get; }
    DbSet<CharacterGraphNodeEntity> CharacterGraphNodes { get; }
    DbSet<CharacterGraphEdgeEntity> CharacterGraphEdges { get; }
    DbSet<CharacterStateEventEntity> CharacterStateEvents { get; }
    DbSet<CharacterStateSnapshotEntity> CharacterStateSnapshots { get; }
    DbSet<CharacterGrowthProposalEntity> CharacterGrowthProposals { get; }
    DbSet<RelationshipStateEventEntity> RelationshipStateEvents { get; }

    DatabaseFacade Database { get; }

    void Detach(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
