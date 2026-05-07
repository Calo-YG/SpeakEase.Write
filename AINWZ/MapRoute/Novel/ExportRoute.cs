using SpeakEase.Write.Application.Novel.Export;

namespace SpeakEase.Write.MapRoute.Novel;

public static class ExportRoute
{
    public static void MapExportEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("novel/export")
            .WithDescription("作品导出")
            .WithTags("export")
            .RequireAuthorization();

        group.MapGet("{workId}/txt", async (
            string workId,
            int? start,
            int? end,
            ExportService exportService,
            CancellationToken ct) =>
        {
            var (content, fileName, contentType) = await exportService.ExportTxtAsync(workId, start, end, ct);
            return Results.File(content, contentType, fileName);
        }).WithName("export_txt");

        group.MapGet("{workId}/epub", async (
            string workId,
            int? start,
            int? end,
            ExportService exportService,
            CancellationToken ct) =>
        {
            var (content, fileName, contentType) = await exportService.ExportEpubAsync(workId, start, end, ct);
            return Results.File(content, contentType, fileName);
        }).WithName("export_epub");
    }
}
