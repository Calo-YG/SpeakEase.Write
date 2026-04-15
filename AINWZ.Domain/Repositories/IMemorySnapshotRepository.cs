using AINWZ.Domain.Entities.Memory;

namespace AINWZ.Application.Repositories;

/// <summary>
/// 记忆快照聚合根仓储。
/// </summary>
public interface IMemorySnapshotRepository : IAggregateRootRepository<MemorySnapshotEntity>
{
}
