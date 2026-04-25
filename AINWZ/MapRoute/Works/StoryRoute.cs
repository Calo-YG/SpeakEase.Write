using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class StoryRoute
{
    public static void MapStoryEndPoint(this IEndpointRouteBuilder app)
    {
        MapChapterEndPoints(app);
        MapCharacterEndPoints(app);
        MapOutlineEndPoints(app);
    }

    private static void MapChapterEndPoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/chapters")
            .WithDescription("章节管理")
            .WithTags("chapters")
            .RequireAuthorization();

        group.MapGet("", async (string workId, IChapterApplication chapterApp, CancellationToken ct) =>
            await chapterApp.ListChaptersAsync(workId, ct))
            .WithName("listchapters");

        group.MapGet("{chapterId}", async (string workId, string chapterId, IChapterApplication chapterApp, CancellationToken ct) =>
            await chapterApp.GetChapterDetailAsync(workId, chapterId, ct))
            .WithName("getchapterdetail");

        group.MapPost("", async (string workId, CreateChapterRequest request, IChapterApplication chapterApp, CancellationToken ct) =>
            await chapterApp.CreateChapterAsync(workId, request, ct))
            .WithName("createchapter");

        group.MapPut("{chapterId}", async (string workId, string chapterId, UpdateChapterRequest request, IChapterApplication chapterApp, CancellationToken ct) =>
            await chapterApp.UpdateChapterAsync(workId, chapterId, request, ct))
            .WithName("updatechapter");

        group.MapDelete("{chapterId}", async (string workId, string chapterId, IChapterApplication chapterApp, CancellationToken ct) =>
            await chapterApp.DeleteChapterAsync(workId, chapterId, ct))
            .WithName("deletechapter");
    }

    private static void MapCharacterEndPoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/characters")
            .WithDescription("角色管理")
            .WithTags("characters")
            .RequireAuthorization();

        group.MapGet("", async (string workId, ICharacterApplication characterApp, CancellationToken ct) =>
            await characterApp.ListCharactersAsync(workId, ct))
            .WithName("listcharacters");

        group.MapGet("{id}", async (string workId, string id, ICharacterApplication characterApp, CancellationToken ct) =>
            await characterApp.GetCharacterByIdAsync(workId, id, ct))
            .WithName("getcharacterbyid");

        group.MapPost("", async (string workId, SaveCharacterRequest request, ICharacterApplication characterApp, CancellationToken ct) =>
            await characterApp.CreateCharacterAsync(workId, request, ct))
            .WithName("createcharacter");

        group.MapPut("{id}", async (string workId, string id, SaveCharacterRequest request, ICharacterApplication characterApp, CancellationToken ct) =>
            await characterApp.UpdateCharacterAsync(workId, id, request, ct))
            .WithName("updatecharacter");

        group.MapDelete("{id}", async (string workId, string id, ICharacterApplication characterApp, CancellationToken ct) =>
            await characterApp.DeleteCharacterAsync(workId, id, ct))
            .WithName("deletecharacter");
    }

    private static void MapOutlineEndPoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/outline")
            .WithDescription("大纲管理")
            .WithTags("outline")
            .RequireAuthorization();

        group.MapGet("", async (string workId, IOutlineApplication outlineApp, CancellationToken ct) =>
            await outlineApp.GetOutlineTreeAsync(workId, ct))
            .WithName("getoutlinetree");

        group.MapPost("", async (string workId, SaveOutlineNodeRequest request, IOutlineApplication outlineApp, CancellationToken ct) =>
            await outlineApp.CreateNodeAsync(workId, request, ct))
            .WithName("createoutlinenode");

        group.MapPut("{nodeId}", async (string workId, string nodeId, SaveOutlineNodeRequest request, IOutlineApplication outlineApp, CancellationToken ct) =>
            await outlineApp.UpdateNodeAsync(workId, nodeId, request, ct))
            .WithName("updateoutlinenode");

        group.MapDelete("{nodeId}", async (string workId, string nodeId, IOutlineApplication outlineApp, CancellationToken ct) =>
            await outlineApp.DeleteNodeAsync(workId, nodeId, ct))
            .WithName("deleteoutlinenode");
    }
}
