using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class VolumeRoute
{
    public static void MapVolumeEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/volumes")
            .WithDescription("卷管理")
            .WithTags("volumes")
            .RequireAuthorization();

        group.MapGet(string.Empty, async (
            string workId,
            IVolumeApplication app,
            CancellationToken ct) =>
        {
            return await app.ListVolumesAsync(workId, ct);
        }).WithName("list_volumes");

        group.MapPost(string.Empty, async (
            string workId,
            CreateVolumeRequest request,
            IVolumeApplication app,
            CancellationToken ct) =>
        {
            return await app.CreateVolumeAsync(workId, request, ct);
        }).WithName("create_volume");

        group.MapPut("{volumeId}", async (
            string workId,
            string volumeId,
            UpdateVolumeRequest request,
            IVolumeApplication app,
            CancellationToken ct) =>
        {
            return await app.UpdateVolumeAsync(workId, volumeId, request, ct);
        }).WithName("update_volume");

        group.MapDelete("{volumeId}", async (
            string workId,
            string volumeId,
            IVolumeApplication app,
            CancellationToken ct) =>
        {
            return await app.DeleteVolumeAsync(workId, volumeId, ct);
        }).WithName("delete_volume");

        group.MapPost("{volumeId}/merge", async (
            string workId,
            string volumeId,
            MergeVolumeRequest request,
            IVolumeApplication app,
            CancellationToken ct) =>
        {
            return await app.MergeVolumesAsync(workId, volumeId, request, ct);
        }).WithName("merge_volumes");

        group.MapPost("move-chapter/{chapterId}/to/{targetVolumeId}", async (
            string workId,
            string chapterId,
            string targetVolumeId,
            IVolumeApplication app,
            CancellationToken ct) =>
        {
            return await app.MoveChapterAsync(workId, chapterId, targetVolumeId, ct);
        }).WithName("move_chapter_to_volume");

        group.MapPost("remove-chapter/{chapterId}", async (
            string workId,
            string chapterId,
            IVolumeApplication app,
            CancellationToken ct) =>
        {
            return await app.RemoveChapterFromVolumeAsync(workId, chapterId, ct);
        }).WithName("remove_chapter_from_volume");
    }
}
