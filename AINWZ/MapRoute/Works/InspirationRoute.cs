using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class InspirationRoute
{
    public static void MapInspirationEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/inspirations")
            .WithDescription("灵感与参考管理")
            .WithTags("inspirations")
            .RequireAuthorization();

        group.MapGet(string.Empty, async (
            string workId, IInspirationApplication app, CancellationToken ct) =>
            await app.ListInspirationsAsync(workId, ct)).WithName("list_inspirations");

        group.MapPost(string.Empty, async (
            string workId, SaveInspirationRequest req, IInspirationApplication app, CancellationToken ct) =>
            await app.CreateInspirationAsync(workId, req, ct)).WithName("create_inspiration");

        group.MapPut("{id}", async (
            string workId, string id, SaveInspirationRequest req, IInspirationApplication app, CancellationToken ct) =>
            await app.UpdateInspirationAsync(workId, id, req, ct)).WithName("update_inspiration");

        group.MapDelete("{id}", async (
            string workId, string id, IInspirationApplication app, CancellationToken ct) =>
            await app.DeleteInspirationAsync(workId, id, ct)).WithName("delete_inspiration");

        group.MapPost("{id}/archive", async (
            string workId, string id, ArchiveInspirationRequest req, IInspirationApplication app, CancellationToken ct) =>
            await app.ArchiveInspirationAsync(workId, id, req, ct)).WithName("archive_inspiration");
    }
}
