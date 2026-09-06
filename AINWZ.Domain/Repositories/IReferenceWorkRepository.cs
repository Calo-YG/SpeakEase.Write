using SpeakEase.Write.Domain.Entities.Learning;
using SpeakEase.Write.Domain.Repositories;

namespace SpeakEase.Write.Domain.Repositories;

/// <summary>
/// 参考作品聚合根仓储。
/// </summary>
public interface IReferenceWorkRepository : IAggregateRootRepository<ReferenceWorkEntity>
{
}
