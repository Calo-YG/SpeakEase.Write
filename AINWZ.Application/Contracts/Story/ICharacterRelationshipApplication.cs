using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Contracts.Story;

public interface ICharacterRelationshipApplication
{
    Task<ApiResult<List<CharacterRelationshipResponse>>> ListRelationshipsAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<CharacterRelationshipResponse>> CreateRelationshipAsync(string workId, SaveCharacterRelationshipRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<CharacterRelationshipResponse>> UpdateRelationshipAsync(string workId, string id, SaveCharacterRelationshipRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteRelationshipAsync(string workId, string id, CancellationToken cancellationToken = default);
    Task<ApiResult<Dictionary<string, List<string>>>> DetectCirclesAsync(string workId, CancellationToken cancellationToken = default);
}
