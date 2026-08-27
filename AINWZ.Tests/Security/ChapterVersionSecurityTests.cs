using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.Write.Application.Applications;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Persistence;
using AINWZ.Tests.AI;

namespace AINWZ.Tests.Security;

public sealed class ChapterVersionSecurityTests
{
    [Fact]
    public async Task ListVersionsAsync_RejectsAChapterFromAnotherUsersWork()
    {
        await using var db = new SpeakEaseDbContext(new DbContextOptionsBuilder<SpeakEaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Works.Add(new WorkEntity { Id = "work-2", UserId = "user-2", Title = "Private" });
        db.Chapters.Add(new ChapterEntity { Id = "chapter-2", WorkId = "work-2", Title = "Private chapter" });
        db.ChapterVersions.Add(new ChapterVersionEntity
        {
            Id = "version-2", ChapterId = "chapter-2", OwnerId = "user-2", VersionNumber = 1
        });
        await db.SaveChangesAsync();

        var manager = new ChapterVersionManager(
            db,
            new SequentialIdGenerator(),
            new TestUserContext("user-1"),
            NullLogger<ChapterVersionManager>.Instance);

        var result = await manager.ListVersionsAsync("work-2", "chapter-2");

        Assert.False(result.Successed);
        Assert.Equal(404, result.Status);
    }
}
