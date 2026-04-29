using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class WorldRoute
{
    public static void MapWorldEndPoint(this IEndpointRouteBuilder app)
    {
        var wsGroup = app.MapGroup("api/works/{workId}/world-setting")
            .WithDescription("世界观设定管理")
            .WithTags("world")
            .RequireAuthorization();

        wsGroup.MapGet(string.Empty, async (string workId, IWorldApplication app, CancellationToken ct) =>
            await app.GetOrCreateWorldSettingAsync(workId, ct)).WithName("get_world_setting");

        wsGroup.MapPut(string.Empty, async (string workId, SaveWorldSettingRequest req, IWorldApplication app, CancellationToken ct) =>
            await app.UpdateWorldSettingAsync(workId, req, ct)).WithName("update_world_setting");

        wsGroup.MapGet("sub-entity-counts", async (string workId, IWorldApplication app, CancellationToken ct) =>
            await app.GetSubEntityCountsAsync(workId, "", ct)).WithName("world_setting_sub_entity_counts");

        var geoGroup = app.MapGroup("api/works/{workId}/geographies")
            .WithDescription("地理管理").WithTags("world").RequireAuthorization();

        geoGroup.MapGet(string.Empty, async (string workId, IWorldApplication app, CancellationToken ct) =>
            await app.ListGeographiesAsync(workId, ct)).WithName("list_geographies");
        geoGroup.MapPost(string.Empty, async (string workId, SaveGeographyRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.CreateGeographyAsync(workId, r, ct)).WithName("create_geography");
        geoGroup.MapPut("{id}", async (string workId, string id, SaveGeographyRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.UpdateGeographyAsync(workId, id, r, ct)).WithName("update_geography");
        geoGroup.MapDelete("{id}", async (string workId, string id, IWorldApplication app, CancellationToken ct) =>
            await app.DeleteGeographyAsync(workId, id, ct)).WithName("delete_geography");

        var facGroup = app.MapGroup("api/works/{workId}/factions")
            .WithDescription("势力管理").WithTags("world").RequireAuthorization();

        facGroup.MapGet(string.Empty, async (string workId, IWorldApplication app, CancellationToken ct) =>
            await app.ListFactionsAsync(workId, ct)).WithName("list_factions");
        facGroup.MapPost(string.Empty, async (string workId, SaveFactionRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.CreateFactionAsync(workId, r, ct)).WithName("create_faction");
        facGroup.MapPut("{id}", async (string workId, string id, SaveFactionRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.UpdateFactionAsync(workId, id, r, ct)).WithName("update_faction");
        facGroup.MapDelete("{id}", async (string workId, string id, IWorldApplication app, CancellationToken ct) =>
            await app.DeleteFactionAsync(workId, id, ct)).WithName("delete_faction");

        var psGroup = app.MapGroup("api/works/{workId}/power-systems")
            .WithDescription("力量体系管理").WithTags("world").RequireAuthorization();

        psGroup.MapGet(string.Empty, async (string workId, IWorldApplication app, CancellationToken ct) =>
            await app.ListPowerSystemsAsync(workId, ct)).WithName("list_power_systems");
        psGroup.MapPost(string.Empty, async (string workId, SavePowerSystemRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.CreatePowerSystemAsync(workId, r, ct)).WithName("create_power_system");
        psGroup.MapPut("{id}", async (string workId, string id, SavePowerSystemRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.UpdatePowerSystemAsync(workId, id, r, ct)).WithName("update_power_system");
        psGroup.MapDelete("{id}", async (string workId, string id, IWorldApplication app, CancellationToken ct) =>
            await app.DeletePowerSystemAsync(workId, id, ct)).WithName("delete_power_system");

        var wrGroup = app.MapGroup("api/works/{workId}/world-rules")
            .WithDescription("世界规则管理").WithTags("world").RequireAuthorization();

        wrGroup.MapGet(string.Empty, async (string workId, IWorldApplication app, CancellationToken ct) =>
            await app.ListWorldRulesAsync(workId, ct)).WithName("list_world_rules");
        wrGroup.MapPost(string.Empty, async (string workId, SaveWorldRuleRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.CreateWorldRuleAsync(workId, r, ct)).WithName("create_world_rule");
        wrGroup.MapPut("{id}", async (string workId, string id, SaveWorldRuleRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.UpdateWorldRuleAsync(workId, id, r, ct)).WithName("update_world_rule");
        wrGroup.MapDelete("{id}", async (string workId, string id, IWorldApplication app, CancellationToken ct) =>
            await app.DeleteWorldRuleAsync(workId, id, ct)).WithName("delete_world_rule");

        var heGroup = app.MapGroup("api/works/{workId}/historical-events")
            .WithDescription("历史事件管理").WithTags("world").RequireAuthorization();

        heGroup.MapGet(string.Empty, async (string workId, IWorldApplication app, CancellationToken ct) =>
            await app.ListHistoricalEventsAsync(workId, ct)).WithName("list_historical_events");
        heGroup.MapPost(string.Empty, async (string workId, SaveHistoricalEventRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.CreateHistoricalEventAsync(workId, r, ct)).WithName("create_historical_event");
        heGroup.MapPut("{id}", async (string workId, string id, SaveHistoricalEventRequest r, IWorldApplication app, CancellationToken ct) =>
            await app.UpdateHistoricalEventAsync(workId, id, r, ct)).WithName("update_historical_event");
        heGroup.MapDelete("{id}", async (string workId, string id, IWorldApplication app, CancellationToken ct) =>
            await app.DeleteHistoricalEventAsync(workId, id, ct)).WithName("delete_historical_event");
    }
}
