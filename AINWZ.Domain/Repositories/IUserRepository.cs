using SpeakEase.Write.Domain.Entities.Users;
using SpeakEase.Write.Domain.Repositories;

namespace SpeakEase.Write.Application.Repositories;

/// <summary>
/// 用户聚合根仓储。
/// </summary>
public interface IUserRepository : IAggregateRootRepository<UserEntity>
{
}
