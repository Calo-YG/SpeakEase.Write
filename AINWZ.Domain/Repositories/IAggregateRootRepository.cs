using AINWZ.Domain;

namespace AINWZ.Application.Repositories;

/// <summary>
/// 聚合根仓储基础抽象，仅面向聚合根实体开放持久化能力。
/// </summary>
/// <typeparam name="TAggregateRoot">聚合根类型。</typeparam>
public interface IAggregateRootRepository<TAggregateRoot> where TAggregateRoot : AggregateRootEntity
{
    /// <summary>
    /// 根据标识获取聚合根。
    /// </summary>
    Task<TAggregateRoot> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取全部聚合根。
    /// </summary>
    Task<List<TAggregateRoot>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增聚合根。
    /// </summary>
    Task AddAsync(TAggregateRoot aggregateRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新聚合根。
    /// </summary>
    Task UpdateAsync(TAggregateRoot aggregateRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除聚合根。
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
