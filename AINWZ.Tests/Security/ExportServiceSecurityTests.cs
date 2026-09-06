using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Application.Novel.Export;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Persistence;
using AINWZ.Tests.AI;

namespace AINWZ.Tests.Security;

public sealed class ExportServiceSecurityTests
{
    [Fact]
    public async Task ExportTxtAsync_RejectsAWorkOwnedByAnotherUser()
    {
        await using var db = new SpeakEaseDbContext(new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Works.Add(new WorkEntity { Id = "work-2", UserId = "user-2", Title = "Private" });
        await db.SaveChangesAsync();

        var service = new ExportService(db, new TestUserContext("user-1"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ExportTxtAsync("work-2"));
    }
}
