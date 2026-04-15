using AINWZ.Domain.Entities.Works;

namespace AINWZ.Application.Repositories;

/// <summary>
/// 章节聚合根仓储。
/// </summary>
public interface IChapterRepository : IAggregateRootRepository<ChapterEntity>
{
}
