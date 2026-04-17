using AINWZ.Application.Contracts.AI.Dto;
using AINWZ.Infrastructure.Shared;

namespace AINWZ.Application.Contracts.Users
{
    /// <summary>
    /// 用户模型配置应用服务接口（主表：UserAiModelConfigEntity，关联：AIModelDefinitionEntity）。
    /// 
    /// 实体职责：
    /// - UserAiModelConfigEntity 是用户级配置，记录用户选择的提供商与模型组合。
    /// - 支持多配置（如"日常续写"、"深度分析"），但同一用户只能有一个激活配置。
    /// - ProviderId / FallbackProviderId 关联 AIModelDefinitionEntity，提供商展示名通过 Join 查询获取。
    /// </summary>
    public interface IUserModelConfigApplication
    {
        /// <summary>
        /// 获取当前用户的模型配置列表。
        /// </summary>
        Task<ApiResult<List<UserModelConfigResponse>>> GetMyConfigsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取当前用户的激活配置。
        /// </summary>
        Task<ApiResult<UserModelConfigResponse>> GetActiveConfigAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 保存（创建或更新）用户模型配置。
        /// 创建时 Id 为空，更新时 Id 非空。
        /// 若用户尚无配置，新建的第一个自动激活。
        /// </summary>
        Task<ApiResult<UserModelConfigResponse>> SaveConfigAsync(SaveUserModelConfigRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 激活指定配置（同时取消同用户其他配置的激活状态）。
        /// </summary>
        Task<ApiResult<UserModelConfigResponse>> ActivateConfigAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 删除用户模型配置。
        /// 若删除的是激活配置，自动激活用户最近的一条配置。
        /// </summary>
        Task<ApiResult> DeleteConfigAsync(string id, CancellationToken cancellationToken = default);
    }
}
