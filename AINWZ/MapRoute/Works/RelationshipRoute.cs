using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class RelationshipRoute
{
    public static void MapRelationshipEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/relationships")
            .WithDescription("角色关系图谱管理")
            .WithTags("relationships")
            .RequireAuthorization();

        group.MapGet(string.Empty, async (
            string workId, ICharacterRelationshipApplication app, CancellationToken ct) =>
            await app.ListRelationshipsAsync(workId, ct)).WithName("list_relationships");

        group.MapPost(string.Empty, async (
            string workId, SaveCharacterRelationshipRequest req, ICharacterRelationshipApplication app, CancellationToken ct) =>
            await app.CreateRelationshipAsync(workId, req, ct)).WithName("create_relationship");

        group.MapPut("{id}", async (
            string workId, string id, SaveCharacterRelationshipRequest req, ICharacterRelationshipApplication app, CancellationToken ct) =>
            await app.UpdateRelationshipAsync(workId, id, req, ct)).WithName("update_relationship");

        group.MapDelete("{id}", async (
            string workId, string id, ICharacterRelationshipApplication app, CancellationToken ct) =>
            await app.DeleteRelationshipAsync(workId, id, ct)).WithName("delete_relationship");

        group.MapGet("circles", async (
            string workId, ICharacterRelationshipApplication app, CancellationToken ct) =>
            await app.DetectCirclesAsync(workId, ct)).WithName("detect_relationship_circles");
    }
}
