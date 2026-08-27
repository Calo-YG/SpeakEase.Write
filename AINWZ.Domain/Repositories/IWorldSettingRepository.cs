using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Domain.Repositories;

namespace SpeakEase.Write.Domain.Repositories;

/// <summary>
/// 世界观聚合根仓储。
/// </summary>
public interface IWorldSettingRepository : IAggregateRootRepository<WorldSettingEntity>
{
}
