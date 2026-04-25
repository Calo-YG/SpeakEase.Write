using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Story;

/// <summary>
/// 章节管理应用服务接口。
/// </summary>
public interface IChapterApplication
{
    Task<ApiResult<List<ChapterItemResponse>>> ListChaptersAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<ChapterDetailResponse>> GetChapterDetailAsync(string workId, string chapterId, CancellationToken cancellationToken = default);
    Task<ApiResult<ChapterDetailResponse>> CreateChapterAsync(string workId, CreateChapterRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<ChapterDetailResponse>> UpdateChapterAsync(string workId, string chapterId, UpdateChapterRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteChapterAsync(string workId, string chapterId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 角色管理应用服务接口。
/// </summary>
public interface ICharacterApplication
{
    Task<ApiResult<List<CharacterItemResponse>>> ListCharactersAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<CharacterItemResponse>> GetCharacterByIdAsync(string workId, string id, CancellationToken cancellationToken = default);
    Task<ApiResult<CharacterItemResponse>> CreateCharacterAsync(string workId, SaveCharacterRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<CharacterItemResponse>> UpdateCharacterAsync(string workId, string id, SaveCharacterRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteCharacterAsync(string workId, string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// 大纲管理应用服务接口。
/// </summary>
public interface IOutlineApplication
{
    Task<ApiResult<List<OutlineNodeItemResponse>>> GetOutlineTreeAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<OutlineNodeItemResponse>> CreateNodeAsync(string workId, SaveOutlineNodeRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<OutlineNodeItemResponse>> UpdateNodeAsync(string workId, string nodeId, SaveOutlineNodeRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteNodeAsync(string workId, string nodeId, CancellationToken cancellationToken = default);
}
