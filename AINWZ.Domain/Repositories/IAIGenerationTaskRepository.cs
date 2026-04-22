using SpeakEase.Write.Domain.Entities.AI;

namespace SpeakEase.Write.Domain.Repositories;

/// <summary>
/// AI 生成任务聚合根仓储。
/// </summary>
public interface IAIGenerationTaskRepository : IAggregateRootRepository<AIGenerationTaskEntity>
{
}
