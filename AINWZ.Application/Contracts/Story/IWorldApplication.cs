using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Story;

public interface IWorldApplication
{
    Task<ApiResult<WorldSettingResponse>> GetOrCreateWorldSettingAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<WorldSettingResponse>> UpdateWorldSettingAsync(string workId, SaveWorldSettingRequest request, CancellationToken cancellationToken = default);

    Task<ApiResult<List<GeographyResponse>>> ListGeographiesAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<GeographyResponse>> CreateGeographyAsync(string workId, SaveGeographyRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<GeographyResponse>> UpdateGeographyAsync(string workId, string id, SaveGeographyRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteGeographyAsync(string workId, string id, CancellationToken cancellationToken = default);

    Task<ApiResult<List<FactionResponse>>> ListFactionsAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<FactionResponse>> CreateFactionAsync(string workId, SaveFactionRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<FactionResponse>> UpdateFactionAsync(string workId, string id, SaveFactionRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteFactionAsync(string workId, string id, CancellationToken cancellationToken = default);

    Task<ApiResult<List<PowerSystemResponse>>> ListPowerSystemsAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<PowerSystemResponse>> CreatePowerSystemAsync(string workId, SavePowerSystemRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<PowerSystemResponse>> UpdatePowerSystemAsync(string workId, string id, SavePowerSystemRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeletePowerSystemAsync(string workId, string id, CancellationToken cancellationToken = default);

    Task<ApiResult<List<WorldRuleResponse>>> ListWorldRulesAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<WorldRuleResponse>> CreateWorldRuleAsync(string workId, SaveWorldRuleRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<WorldRuleResponse>> UpdateWorldRuleAsync(string workId, string id, SaveWorldRuleRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteWorldRuleAsync(string workId, string id, CancellationToken cancellationToken = default);

    Task<ApiResult<List<HistoricalEventResponse>>> ListHistoricalEventsAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<HistoricalEventResponse>> CreateHistoricalEventAsync(string workId, SaveHistoricalEventRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<HistoricalEventResponse>> UpdateHistoricalEventAsync(string workId, string id, SaveHistoricalEventRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteHistoricalEventAsync(string workId, string id, CancellationToken cancellationToken = default);

    Task<ApiResult<Dictionary<string, int>>> GetSubEntityCountsAsync(string workId, string worldSettingId, CancellationToken cancellationToken = default);
}
