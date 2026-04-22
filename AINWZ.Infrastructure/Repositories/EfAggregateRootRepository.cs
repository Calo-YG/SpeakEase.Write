using SpeakEase.Write.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Domain;
using SpeakEase.Write.Domain.Repositories;

namespace SpeakEase.Write.Infrastructure.Repositories;

/// <summary>
/// EF Core 聚合根仓储基础实现。
/// </summary>
public class EfAggregateRootRepository<TAggregateRoot>(SpeakEaseDbContext dbContext) : IAggregateRootRepository<TAggregateRoot>
    where TAggregateRoot : AggregateRootEntity
{
    protected readonly SpeakEaseDbContext DbContext = dbContext;

    public async Task<TAggregateRoot> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await DbContext.Set<TAggregateRoot>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<List<TAggregateRoot>> GetAllAsync(CancellationToken cancellationToken = default)
        => await DbContext.Set<TAggregateRoot>().ToListAsync(cancellationToken);

    public async Task AddAsync(TAggregateRoot aggregateRoot, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<TAggregateRoot>().AddAsync(aggregateRoot, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TAggregateRoot aggregateRoot, CancellationToken cancellationToken = default)
    {
        DbContext.Set<TAggregateRoot>().Update(aggregateRoot);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        DbContext.Set<TAggregateRoot>().Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
