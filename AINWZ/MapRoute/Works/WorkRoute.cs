using SpeakEase.Write.Application.Contracts.Works;
using SpeakEase.Write.Application.Contracts.Works.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class WorkRoute
{
    public static void MapWorkEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/work")
            .WithDescription("作品管理")
            .WithTags("work")
            .RequireAuthorization();

        group.MapPost("query", async (WorkQueryRequest request, IWorkApplication workApp, CancellationToken cancellationToken) =>
        {
            return await workApp.QueryWorksAsync(request, cancellationToken);
        }).WithName("queryworks");

        group.MapGet("{id}", async (string id, IWorkApplication workApp, CancellationToken cancellationToken) =>
        {
            return await workApp.GetByIdAsync(id, cancellationToken);
        }).WithName("getworkbyid");

        group.MapPost("", async (CreateWorkRequest request, IWorkApplication workApp, CancellationToken cancellationToken) =>
        {
            return await workApp.CreateWorkAsync(request, cancellationToken);
        }).WithName("creatework");

        group.MapPut("{id}", async (string id, UpdateWorkRequest request, IWorkApplication workApp, CancellationToken cancellationToken) =>
        {
            return await workApp.UpdateWorkAsync(id, request, cancellationToken);
        }).WithName("updatework");

        group.MapDelete("{id}", async (string id, IWorkApplication workApp, CancellationToken cancellationToken) =>
        {
            return await workApp.DeleteWorkAsync(id, cancellationToken);
        }).WithName("deletework");
    }
}
