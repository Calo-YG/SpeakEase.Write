using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class TimelineRoute
{
    public static void MapTimelineEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/timeline")
            .WithDescription("时间线管理")
            .WithTags("timeline")
            .RequireAuthorization();

        group.MapGet(string.Empty, async (
            string workId,
            ITimelineApplication app,
            CancellationToken ct) =>
        {
            return await app.ListTimelineEventsAsync(workId, ct);
        }).WithName("list_timeline_events");

        group.MapGet("{id}", async (
            string workId,
            string id,
            ITimelineApplication app,
            CancellationToken ct) =>
        {
            return await app.GetTimelineEventByIdAsync(workId, id, ct);
        }).WithName("get_timeline_event");

        group.MapPost(string.Empty, async (
            string workId,
            SaveTimelineEventRequest request,
            ITimelineApplication app,
            CancellationToken ct) =>
        {
            return await app.CreateTimelineEventAsync(workId, request, ct);
        }).WithName("create_timeline_event");

        group.MapPut("{id}", async (
            string workId,
            string id,
            SaveTimelineEventRequest request,
            ITimelineApplication app,
            CancellationToken ct) =>
        {
            return await app.UpdateTimelineEventAsync(workId, id, request, ct);
        }).WithName("update_timeline_event");

        group.MapDelete("{id}", async (
            string workId,
            string id,
            ITimelineApplication app,
            CancellationToken ct) =>
        {
            return await app.DeleteTimelineEventAsync(workId, id, ct);
        }).WithName("delete_timeline_event");

        group.MapGet("{id}/dependents", async (
            string workId,
            string id,
            ITimelineApplication app,
            CancellationToken ct) =>
        {
            return await app.ListEventsBeforeDeleteAsync(workId, id, ct);
        }).WithName("timeline_event_dependents");
    }
}
