using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Works;

namespace SpeakEase.Write.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for creation session lifecycle and conversation messages.
/// </summary>
public interface ICreationSessionDbContext
{
    DbSet<AICreationSessionEntity> AICreationSessions { get; }
    DbSet<AICreationMessageEntity> AICreationMessages { get; }
    DbSet<WorkEntity> Works { get; }

    DatabaseFacade Database { get; }

    void Detach(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
