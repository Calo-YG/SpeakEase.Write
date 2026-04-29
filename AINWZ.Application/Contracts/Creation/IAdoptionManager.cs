using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Creation;

public interface IAdoptionManager
{
    Task<ApiResult<ChapterDetailResponse>> AdoptFullAsync(AdoptChapterRequest request);
    Task<ApiResult> DiscardAsync(string sessionId);
}
