using AINWZ.Domain.Entities.AI;

namespace AINWZ.Application.Repositories;

/// <summary>
/// AI 生成任务聚合根仓储。
/// </summary>
public interface IAIGenerationTaskRepository : IAggregateRootRepository<AIGenerationTaskEntity>
{
}
