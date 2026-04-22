using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Domain.Repositories;

namespace SpeakEase.Write.Application.Repositories;

/// <summary>
/// 记忆快照聚合根仓储。
/// </summary>
public interface IMemorySnapshotRepository : IAggregateRootRepository<MemorySnapshotEntity>
{
}
