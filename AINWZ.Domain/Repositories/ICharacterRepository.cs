using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Domain.Repositories;

namespace SpeakEase.Write.Domain.Repositories;

/// <summary>
/// 角色聚合根仓储。
/// </summary>
public interface ICharacterRepository : IAggregateRootRepository<CharacterEntity>
{
}
