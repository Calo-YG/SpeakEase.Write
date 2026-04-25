using SpeakEase.Write.Application.Contracts.Dashboard;

namespace SpeakEase.Write.MapRoute.Dashboard;

public static class DashboardRoute
{
    public static void MapDashboardEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/dashboard")
            .WithDescription("仪表板")
            .WithTags("dashboard")
            .RequireAuthorization();

        group.MapGet("stats", async (IDashboardApplication dashApp, CancellationToken ct) =>
            await dashApp.GetStatsAsync(ct))
            .WithName("getdashboardstats");

        group.MapGet("recent-works", async (int? limit, IDashboardApplication dashApp, CancellationToken ct) =>
            await dashApp.GetRecentWorksAsync(limit ?? 5, ct))
            .WithName("getrecentworks");
    }
}
