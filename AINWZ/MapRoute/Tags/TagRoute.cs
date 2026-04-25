using SpeakEase.Write.Application.Contracts.Tags;
using SpeakEase.Write.Application.Contracts.Tags.Dto;

namespace SpeakEase.Write.MapRoute.Tags;

public static class TagRoute
{
    public static void MapTagEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/tags")
            .WithDescription("标签管理")
            .WithTags("tags")
            .RequireAuthorization();

        group.MapGet("", async (string category, ITagApplication tagApp, CancellationToken ct) =>
            await tagApp.ListTagsAsync(category, ct))
            .WithName("listtags");

        group.MapGet("hot", async (int? limit, ITagApplication tagApp, CancellationToken ct) =>
            await tagApp.GetHotTagsAsync(limit ?? 20, ct))
            .WithName("gethottags");

        group.MapPost("", async (SaveTagRequest request, ITagApplication tagApp, CancellationToken ct) =>
            await tagApp.CreateTagAsync(request, ct))
            .WithName("createtag");

        group.MapPut("{id}", async (string id, SaveTagRequest request, ITagApplication tagApp, CancellationToken ct) =>
            await tagApp.UpdateTagAsync(id, request, ct))
            .WithName("updatetag");

        group.MapDelete("{id}", async (string id, ITagApplication tagApp, CancellationToken ct) =>
            await tagApp.DeleteTagAsync(id, ct))
            .WithName("deletetag");
    }
}
