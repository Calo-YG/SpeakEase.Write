using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;

namespace SpeakEase.Write.MapRoute.AI;

public static class AdoptionRoute
{
    public static void MapAdoptionEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/creation/adopt")
            .WithDescription("AI内容采纳管理")
            .WithTags("creation")
            .RequireAuthorization();

        group.MapPost("full", async (
            AdoptChapterRequest request,
            IAdoptionManager mgr) =>
        {
            return await mgr.AdoptFullAsync(request);
        }).WithName("adopt_full");

        group.MapPost("{sessionId}/discard", async (
            string sessionId,
            IAdoptionManager mgr) =>
        {
            return await mgr.DiscardAsync(sessionId);
        }).WithName("discard_adoption");
    }
}
