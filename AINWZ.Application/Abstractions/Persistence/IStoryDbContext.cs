using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Domain.Entities.Works;

namespace SpeakEase.Write.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for chapter, volume and outline authoring data.
/// </summary>
public interface IStoryDbContext
{
    DbSet<WorkEntity> Works { get; }
    DbSet<VolumeEntity> Volumes { get; }
    DbSet<ChapterEntity> Chapters { get; }
    DbSet<ChapterVersionEntity> ChapterVersions { get; }
    DbSet<OutlineEntity> Outlines { get; }
    DbSet<OutlineNodeEntity> OutlineNodes { get; }

    DatabaseFacade Database { get; }

    void Detach(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
