using AINWZ.Application.Contracts.AI.Dto;
using AINWZ.Infrastructure.Shared;

namespace AINWZ.Application.Contracts.AI
{
    /// <summary>
    /// 模型提供商管理应用服务接口（单表：AIModelDefinitionEntity）。
    /// </summary>
    public interface IModelApplication
    {
        /// <summary>
        /// 获取所有提供商列表。
        /// </summary>
        Task<ApiResult<List<ModelProviderResponse>>> GetProvidersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据标识获取提供商。
        /// </summary>
        Task<ApiResult<ModelProviderResponse>> GetProviderByIdAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 创建提供商。
        /// </summary>
        Task<ApiResult<ModelProviderResponse>> CreateProviderAsync(SaveProviderRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新提供商。
        /// </summary>
        Task<ApiResult<ModelProviderResponse>> UpdateProviderAsync(string id, SaveProviderRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 删除提供商。
        /// </summary>
        Task<ApiResult> DeleteProviderAsync(string id, CancellationToken cancellationToken = default);
    }
}
