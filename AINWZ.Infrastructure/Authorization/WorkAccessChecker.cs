using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Application.Abstractions.Authorization;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.Authorization;

public sealed class WorkAccessChecker(IWriteDbContext db) : IWorkAccessChecker
{
    public Task<bool> OwnsWorkAsync(
        string workId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workId) || string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(false);

        return db.Works
            .AsNoTracking()
            .AnyAsync(x => x.Id == workId && x.UserId == userId, cancellationToken);
    }
}
