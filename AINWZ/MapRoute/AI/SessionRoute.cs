using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;

namespace SpeakEase.Write.MapRoute.AI;

public static class SessionRoute
{
    public static void MapSessionEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/creation/sessions")
            .WithDescription("创作会话管理")
            .WithTags("creation")
            .RequireAuthorization();

        group.MapPost("start/{workId}", async (
            string workId,
            ICreationSessionManager mgr) =>
        {
            return await mgr.StartSessionAsync(workId);
        }).WithName("start_session");

        group.MapPost("{sessionId}/turn", async (
            string sessionId,
            ICreationSessionManager mgr) =>
        {
            return await mgr.RecordTurnAsync(sessionId);
        }).WithName("record_turn");

        group.MapPost("{sessionId}/adopt", async (
            string sessionId,
            AdoptContentRequest request,
            ICreationSessionManager mgr) =>
        {
            return await mgr.AdoptContentAsync(sessionId, request);
        }).WithName("adopt_content");

        group.MapPost("{sessionId}/pause", async (
            string sessionId,
            ICreationSessionManager mgr) =>
        {
            return await mgr.PauseSessionAsync(sessionId);
        }).WithName("pause_session");

        group.MapPost("{sessionId}/cancel", async (
            string sessionId,
            ICreationSessionManager mgr) =>
        {
            return await mgr.CancelSessionAsync(sessionId);
        }).WithName("cancel_session");

        group.MapPost("{sessionId}/resume", async (
            string sessionId,
            ICreationSessionManager mgr) =>
        {
            return await mgr.ResumeSessionAsync(sessionId);
        }).WithName("resume_session");

        group.MapPost("{sessionId}/rollback/{targetTurn:int}", async (
            string sessionId,
            int targetTurn,
            ICreationSessionManager mgr) =>
        {
            return await mgr.RollbackToTurnAsync(sessionId, targetTurn);
        }).WithName("rollback_session");

        group.MapGet("active/{workId}", async (
            string workId,
            ICreationSessionManager mgr) =>
        {
            return await mgr.GetActiveSessionAsync(workId);
        }).WithName("get_active_session");

        group.MapGet("list/{workId}", async (
            string workId,
            ICreationSessionManager mgr) =>
        {
            return await mgr.ListSessionsAsync(workId);
        }).WithName("list_sessions");

        group.MapGet("{sessionId}/messages", async (
            string sessionId,
            int? limit,
            ICreationSessionManager mgr) =>
        {
            return await mgr.GetSessionMessagesAsync(sessionId, limit);
        }).WithName("get_session_messages");

        group.MapPost("expire-stale", async (
            ICreationSessionManager mgr) =>
        {
            var count = await mgr.ExpireStaleSessionsAsync();
            return Results.Ok(new { expiredCount = count });
        }).WithName("expire_stale_sessions");
    }
}
