using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class ForeshadowingRoute
{
    public static void MapForeshadowingEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/foreshadowings")
            .WithDescription("伏笔管理")
            .WithTags("foreshadowings")
            .RequireAuthorization();

        group.MapGet(string.Empty, async (
            string workId,
            IForeshadowingApplication app,
            CancellationToken ct) =>
        {
            return await app.ListForeshadowingsAsync(workId, cancellationToken: ct);
        }).WithName("list_foreshadowings");

        group.MapGet("{id}", async (
            string workId,
            string id,
            IForeshadowingApplication app,
            CancellationToken ct) =>
        {
            return await app.GetForeshadowingByIdAsync(workId, id, ct);
        }).WithName("get_foreshadowing");

        group.MapPost(string.Empty, async (
            string workId,
            SaveForeshadowingRequest request,
            IForeshadowingApplication app,
            CancellationToken ct) =>
        {
            return await app.CreateForeshadowingAsync(workId, request, ct);
        }).WithName("create_foreshadowing");

        group.MapPut("{id}", async (
            string workId,
            string id,
            SaveForeshadowingRequest request,
            IForeshadowingApplication app,
            CancellationToken ct) =>
        {
            return await app.UpdateForeshadowingAsync(workId, id, request, ct);
        }).WithName("update_foreshadowing");

        group.MapDelete("{id}", async (
            string workId,
            string id,
            IForeshadowingApplication app,
            CancellationToken ct) =>
        {
            return await app.DeleteForeshadowingAsync(workId, id, ct);
        }).WithName("delete_foreshadowing");

        group.MapGet("pending-resolutions", async (
            string workId,
            IForeshadowingApplication app,
            CancellationToken ct) =>
        {
            return await app.ListPendingResolutionsAsync(workId, ct);
        }).WithName("pending_foreshadowing_resolutions");
    }
}
