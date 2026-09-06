using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SpeakEase.Write.Application.Applications;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Application.Contracts.Version;
using SpeakEase.Write.Application.Contracts.Version.Dto;
using SpeakEase.Write.Application.Shared;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.AI;

public sealed class AdoptionConsistencyTests
{
    [Fact]
    public async Task AdoptFullAsync_DoesNotModifyChapterWhenVersionCreationFails()
    {
        await using var db = TestDb.Create();
        db.Works.Add(new WorkEntity { Id = "work-1", UserId = "user-1", Title = "Test" });
        db.Chapters.Add(new ChapterEntity
        {
            Id = "chapter-1", WorkId = "work-1", OwnerId = "user-1", Title = "Chapter", Content = "old"
        });
        await db.SaveChangesAsync();

        var versions = new Mock<IChapterVersionManager>();
        versions.Setup(x => x.CreateVersionAsync(It.IsAny<CreateVersionRequest>()))
            .ReturnsAsync(new ApiResult<ChapterVersionDto>("version failed", 500));
        var sessions = new Mock<ICreationSessionManager>();
        var manager = new AdoptionManager(
            db,
            new TestUserContext("user-1"),
            versions.Object,
            sessions.Object,
            NullLogger<AdoptionManager>.Instance);

        var result = await manager.AdoptFullAsync(new AdoptChapterRequest
        {
            WorkId = "work-1",
            ChapterId = "chapter-1",
            Content = "new content"
        });

        Assert.False(result.Successed);
        var chapter = await db.Chapters.AsNoTracking().SingleAsync(x => x.Id == "chapter-1");
        Assert.Equal("old", chapter.Content);
    }
}
