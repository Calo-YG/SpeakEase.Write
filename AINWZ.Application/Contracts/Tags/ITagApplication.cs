using SpeakEase.Write.Application.Contracts.Tags.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Tags;

/// <summary>
/// 标签管理应用服务接口。
/// </summary>
public interface ITagApplication
{
    Task<ApiResult<List<TagItemResponse>>> ListTagsAsync(string category, CancellationToken cancellationToken = default);
    Task<ApiResult<TagItemResponse>> CreateTagAsync(SaveTagRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<TagItemResponse>> UpdateTagAsync(string id, SaveTagRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteTagAsync(string id, CancellationToken cancellationToken = default);
    Task<ApiResult<List<TagItemResponse>>> GetHotTagsAsync(int limit, CancellationToken cancellationToken = default);
}
