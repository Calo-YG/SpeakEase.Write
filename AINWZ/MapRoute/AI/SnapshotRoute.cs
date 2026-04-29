using SpeakEase.Write.Application.Contracts.Snapshot;

namespace SpeakEase.Write.MapRoute.AI;

public static class SnapshotRoute
{
    public static void MapSnapshotEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/creation/snapshots")
            .WithDescription("黑板上下文快照管理")
            .WithTags("creation")
            .RequireAuthorization();

        group.MapGet("list/{workId}", async (
            string workId,
            ISnapshotService svc) =>
        {
            return await svc.ListSnapshotsAsync(workId);
        }).WithName("list_snapshots");

        group.MapPost("{snapshotId}/restore", async (
            string snapshotId,
            ISnapshotService svc) =>
        {
            return await svc.RestoreSnapshotAsync(snapshotId);
        }).WithName("restore_snapshot");

        group.MapPost("undo/{workId}", async (
            string workId,
            ISnapshotService svc) =>
        {
            return await svc.UndoLastAgentRunAsync(workId);
        }).WithName("undo_last_agent");

        group.MapPost("restore-last/{workId}", async (
            string workId,
            ISnapshotService svc) =>
        {
            return await svc.RestoreLastAfterSnapshotAsync(workId);
        }).WithName("restore_last_after");

        group.MapPost("archive-old", async (
            ISnapshotService svc) =>
        {
            var count = await svc.ArchiveOldSnapshotsAsync();
            return Results.Ok(new { archivedCount = count });
        }).WithName("archive_old_snapshots");
    }
}
