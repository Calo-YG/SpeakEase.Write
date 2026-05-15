using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class CharacterArcRoute
{
    public static void MapCharacterArcEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/characters/{characterId}/arcs")
            .WithDescription("角色成长弧线管理")
            .WithTags("arcs")
            .RequireAuthorization();

        group.MapGet(string.Empty, async (
            string workId, string characterId, ICharacterArcApplication app, CancellationToken ct) =>
            await app.ListArcsByCharacterAsync(workId, characterId, ct)).WithName("list_character_arcs");

        group.MapPost(string.Empty, async (
            string workId, string characterId, SaveCharacterArcRequest req, ICharacterArcApplication app, CancellationToken ct) =>
            await app.CreateArcAsync(workId, characterId, req, ct)).WithName("create_character_arc");

        group.MapPut("{arcId}", async (
            string workId, string characterId, string arcId, SaveCharacterArcRequest req, ICharacterArcApplication app, CancellationToken ct) =>
            await app.UpdateArcAsync(workId, characterId, arcId, req, ct)).WithName("update_character_arc");

        group.MapDelete("{arcId}", async (
            string workId, string characterId, string arcId, ICharacterArcApplication app, CancellationToken ct) =>
            await app.DeleteArcAsync(workId, characterId, arcId, ct)).WithName("delete_character_arc");
    }

    public static void MapAllCharacterArcsEndPoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("api/works/{workId}/arcs", async (
            string workId, ICharacterArcApplication app, CancellationToken ct) =>
            await app.ListAllArcsAsync(workId, ct))
            .WithTags("arcs")
            .RequireAuthorization()
            .WithName("list_all_arcs");
    }
}
