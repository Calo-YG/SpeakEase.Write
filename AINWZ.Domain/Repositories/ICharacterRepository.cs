using AINWZ.Domain.Entities.Story;

namespace AINWZ.Application.Repositories;

/// <summary>
/// 角色聚合根仓储。
/// </summary>
public interface ICharacterRepository : IAggregateRootRepository<CharacterEntity>
{
}
