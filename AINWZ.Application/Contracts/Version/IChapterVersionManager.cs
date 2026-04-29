using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Application.Contracts.Version.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Version;

public interface IChapterVersionManager
{
    Task<ApiResult<ChapterVersionDto>> CreateVersionAsync(CreateVersionRequest request);
    Task<ApiResult<List<ChapterVersionDto>>> ListVersionsAsync(string chapterId);
    Task<ApiResult<ChapterVersionDetailDto>> GetVersionAsync(string versionId);
    Task<ApiResult<ChapterVersionDto>> RollbackToVersionAsync(string chapterId, string targetVersionId);
    Task<ApiResult<ChapterVersionDto>> MergeFromVersionAsync(string chapterId, string sourceVersionId);
    Task<ApiResult> DeleteVersionAsync(string versionId);
    Task<ApiResult<ChapterItemResponse>> SaveAsNewChapterAsync(SaveAsNewChapterRequest request);
}
