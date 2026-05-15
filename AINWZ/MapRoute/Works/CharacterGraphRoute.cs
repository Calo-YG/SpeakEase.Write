using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class CharacterGraphRoute
{
    public static void MapCharacterGraphEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/graphs")
            .WithDescription("角色关系图谱管理")
            .WithTags("graphs")
            .RequireAuthorization();

        group.MapGet(string.Empty, async (
            string workId, ICharacterGraphApplication app, CancellationToken ct) =>
            await app.ListGraphsAsync(workId, ct)).WithName("list_graphs");

        group.MapGet("{graphId}", async (
            string workId, string graphId, ICharacterGraphApplication app, CancellationToken ct) =>
            await app.GetGraphDetailAsync(workId, graphId, ct)).WithName("get_graph_detail");

        group.MapPost(string.Empty, async (
            string workId, SaveCharacterGraphRequest req, ICharacterGraphApplication app, CancellationToken ct) =>
            await app.CreateGraphAsync(workId, req, ct)).WithName("create_graph");

        group.MapDelete("{graphId}", async (
            string workId, string graphId, ICharacterGraphApplication app, CancellationToken ct) =>
            await app.DeleteGraphAsync(workId, graphId, ct)).WithName("delete_graph");

        group.MapPut("{graphId}/layout", async (
            string workId, string graphId, UpdateGraphLayoutRequest req, ICharacterGraphApplication app, CancellationToken ct) =>
            await app.UpdateLayoutAsync(workId, graphId, req, ct)).WithName("update_graph_layout");
    }
}
