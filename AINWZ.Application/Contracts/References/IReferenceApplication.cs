using SpeakEase.Write.Application.Contracts.References.Dto;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Contracts.References;

/// <summary>
/// 参考资源应用服务接口。
/// </summary>
public interface IReferenceApplication
{
    Task<ApiResult<List<ReferenceWorkItemResponse>>> GetWorksAsync(ReferenceWorkQueryRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<PageResult<ReferencePassageItemResponse>>> QueryPassagesAsync(ReferencePassageQueryRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<ReferencePassageItemResponse>> GetPassageByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<ApiResult<ReferencePassageItemResponse>> AddPassageAsync(SaveReferencePassageRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeletePassageAsync(string id, CancellationToken cancellationToken = default);
    Task<ApiResult<bool>> ToggleFavoriteAsync(string passageId, CancellationToken cancellationToken = default);
}
