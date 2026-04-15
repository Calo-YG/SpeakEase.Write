using AINWZ.Domain.Entities.Users;

namespace AINWZ.Application.Repositories;

/// <summary>
/// 用户聚合根仓储。
/// </summary>
public interface IUserRepository : IAggregateRootRepository<UserEntity>
{
}
