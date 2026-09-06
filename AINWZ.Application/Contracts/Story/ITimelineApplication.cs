using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Contracts.Story;

public interface ITimelineApplication
{
    Task<ApiResult<List<TimelineEventItemResponse>>> ListTimelineEventsAsync(string workId, CancellationToken cancellationToken = default);
    Task<ApiResult<TimelineEventItemResponse>> GetTimelineEventByIdAsync(string workId, string id, CancellationToken cancellationToken = default);
    Task<ApiResult<TimelineEventItemResponse>> CreateTimelineEventAsync(string workId, SaveTimelineEventRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<TimelineEventItemResponse>> UpdateTimelineEventAsync(string workId, string id, SaveTimelineEventRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteTimelineEventAsync(string workId, string id, CancellationToken cancellationToken = default);
    Task<ApiResult<List<TimelineEventItemResponse>>> ListEventsBeforeDeleteAsync(string workId, string eventId, CancellationToken cancellationToken = default);
}
