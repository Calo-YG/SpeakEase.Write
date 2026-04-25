using SpeakEase.Write.Application.Contracts.References;
using SpeakEase.Write.Application.Contracts.References.Dto;

namespace SpeakEase.Write.MapRoute.References;

public static class ReferenceRoute
{
    public static void MapReferenceEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/references")
            .WithDescription("参考资源管理")
            .WithTags("references")
            .RequireAuthorization();

        group.MapPost("works", async (ReferenceWorkQueryRequest request, IReferenceApplication refApp, CancellationToken ct) =>
            await refApp.GetWorksAsync(request, ct))
            .WithName("getreferenceworks");

        group.MapPost("passages/query", async (ReferencePassageQueryRequest request, IReferenceApplication refApp, CancellationToken ct) =>
            await refApp.QueryPassagesAsync(request, ct))
            .WithName("querypassages");

        group.MapGet("passages/{id}", async (string id, IReferenceApplication refApp, CancellationToken ct) =>
            await refApp.GetPassageByIdAsync(id, ct))
            .WithName("getpassagebyid");

        group.MapPost("passages", async (SaveReferencePassageRequest request, IReferenceApplication refApp, CancellationToken ct) =>
            await refApp.AddPassageAsync(request, ct))
            .WithName("addpassage");

        group.MapDelete("passages/{id}", async (string id, IReferenceApplication refApp, CancellationToken ct) =>
            await refApp.DeletePassageAsync(id, ct))
            .WithName("deletepassage");

        group.MapPut("passages/{id}/favorite", async (string id, IReferenceApplication refApp, CancellationToken ct) =>
            await refApp.ToggleFavoriteAsync(id, ct))
            .WithName("togglepassagefavorite");
    }
}
