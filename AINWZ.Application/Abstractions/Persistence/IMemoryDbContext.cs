using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Memory;

namespace SpeakEase.Write.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for conversation memory and context assembly state.
/// </summary>
public interface IMemoryDbContext
{
    DbSet<AICreationSessionEntity> AICreationSessions { get; }
    DbSet<AICreationMessageEntity> AICreationMessages { get; }
    DbSet<MemorySnapshotEntity> MemorySnapshots { get; }
    DbSet<MemoryFactEntity> MemoryFacts { get; }
    DbSet<ContextAssemblyLogEntity> ContextAssemblyLogs { get; }

    DatabaseFacade Database { get; }

    void Detach(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
