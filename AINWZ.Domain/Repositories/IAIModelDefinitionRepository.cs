using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Repositories;

namespace SpeakEase.Write.Application.Repositories;

/// <summary>
/// AI 模型定义聚合根仓储。
/// </summary>
public interface IAIModelDefinitionRepository : IAggregateRootRepository<AIModelDefinitionEntity>
{
}
