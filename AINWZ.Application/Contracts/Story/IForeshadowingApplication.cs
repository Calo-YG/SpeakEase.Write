using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Story;

public interface IForeshadowingApplication
{
    Task<ApiResult<List<ForeshadowingItemResponse>>> ListForeshadowingsAsync(string workId, bool? onlyPending = null, CancellationToken cancellationToken = default);
    Task<ApiResult<ForeshadowingItemResponse>> GetForeshadowingByIdAsync(string workId, string id, CancellationToken cancellationToken = default);
    Task<ApiResult<ForeshadowingItemResponse>> CreateForeshadowingAsync(string workId, SaveForeshadowingRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<ForeshadowingItemResponse>> UpdateForeshadowingAsync(string workId, string id, SaveForeshadowingRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteForeshadowingAsync(string workId, string id, CancellationToken cancellationToken = default);
    Task<ApiResult<List<ForeshadowingItemResponse>>> ListPendingResolutionsAsync(string workId, CancellationToken cancellationToken = default);
}
