using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Story;

public interface IInspirationApplication
{
    Task<ApiResult<List<InspirationRecordResponse>>> ListInspirationsAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<InspirationRecordResponse>> CreateInspirationAsync(string workId, SaveInspirationRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<InspirationRecordResponse>> UpdateInspirationAsync(string workId, string id, SaveInspirationRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteInspirationAsync(string workId, string id, CancellationToken cancellationToken = default);
    Task<ApiResult> ArchiveInspirationAsync(string workId, string id, ArchiveInspirationRequest request, CancellationToken cancellationToken = default);
}
