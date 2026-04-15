using AINWZ.Domain.Entities.Story;

namespace AINWZ.Application.Repositories;

/// <summary>
/// 大纲聚合根仓储。
/// </summary>
public interface IOutlineRepository : IAggregateRootRepository<OutlineEntity>
{
}
