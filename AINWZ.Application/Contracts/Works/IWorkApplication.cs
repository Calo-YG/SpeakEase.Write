using SpeakEase.Write.Application.Contracts.Works.Dto;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Contracts.Works;

/// <summary>
/// 作品管理应用服务接口。
/// </summary>
public interface IWorkApplication
{
    Task<ApiResult<PageResult<WorkItemResponse>>> QueryWorksAsync(WorkQueryRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<WorkItemResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<ApiResult<WorkItemResponse>> CreateWorkAsync(CreateWorkRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<WorkItemResponse>> UpdateWorkAsync(string id, UpdateWorkRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteWorkAsync(string id, CancellationToken cancellationToken = default);
}
