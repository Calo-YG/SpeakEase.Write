using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Contracts.Story;

public interface IVolumeApplication
{
    Task<ApiResult<List<VolumeItemResponse>>> ListVolumesAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<VolumeItemResponse>> CreateVolumeAsync(string workId, CreateVolumeRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<VolumeItemResponse>> UpdateVolumeAsync(string workId, string volumeId, UpdateVolumeRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteVolumeAsync(string workId, string volumeId, CancellationToken cancellationToken = default);
    Task<ApiResult> MergeVolumesAsync(string workId, string volumeId, MergeVolumeRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> MoveChapterAsync(string workId, string chapterId, string targetVolumeId, CancellationToken cancellationToken = default);
    Task<ApiResult> RemoveChapterFromVolumeAsync(string workId, string chapterId, CancellationToken cancellationToken = default);
}
