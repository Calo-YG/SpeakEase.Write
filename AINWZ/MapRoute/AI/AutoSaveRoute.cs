using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.AI;

public static class AutoSaveRoute
{
    public static void MapAutoSaveEndPoint(this IEndpointRouteBuilder app)
    {
        app.MapGroup("api/autosave")
            .WithDescription("自动保存")
            .WithTags("autosave")
            .RequireAuthorization()
            .MapPost(string.Empty, async (
                AutoSaveRequest request,
                IAutoSaveApplication app,
                CancellationToken ct) =>
            {
                return await app.AutoSaveAsync(request, ct);
            }).WithName("autosave");
    }
}
