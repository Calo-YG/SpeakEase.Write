using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Domain.Repositories;

namespace SpeakEase.Write.Domain.Repositories;

/// <summary>
/// 章节聚合根仓储。
/// </summary>
public interface IChapterRepository : IAggregateRootRepository<ChapterEntity>
{
}
