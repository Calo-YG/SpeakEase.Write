using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Authorization;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.Security;

public sealed class WorkAccessCheckerTests
{
    [Fact]
    public async Task OwnsWorkAsync_ReturnsTrueOnlyForTheWorkOwner()
    {
        await using var db = new SpeakEaseDbContext(new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Works.Add(new WorkEntity { Id = "work-1", UserId = "user-1", Title = "Test" });
        await db.SaveChangesAsync();

        var checker = new WorkAccessChecker(db);

        Assert.True(await checker.OwnsWorkAsync("work-1", "user-1"));
        Assert.False(await checker.OwnsWorkAsync("work-1", "user-2"));
    }
}
