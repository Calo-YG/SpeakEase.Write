using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Contracts.AI
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

        /// <summary>
        /// 获取指定提供商下的可用模型列表（调用提供商 /models 端点）。
        /// </summary>
        Task<ApiResult<List<string>>> GetProviderModelsAsync(string id, CancellationToken cancellationToken = default);
    }
}
