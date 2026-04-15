using AINWZ.Domain.Entities.Works;

namespace AINWZ.Application.Repositories;

/// <summary>
/// 作品聚合根仓储。
/// </summary>
public interface IWorkRepository : IAggregateRootRepository<WorkEntity>
{
}
