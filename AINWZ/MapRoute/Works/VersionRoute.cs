using SpeakEase.Write.Application.Contracts.Version;
using SpeakEase.Write.Application.Contracts.Version.Dto;

namespace SpeakEase.Write.MapRoute.Works;

public static class VersionRoute
{
    public static void MapVersionEndPoint(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/works/{workId}/chapters/{chapterId}/versions")
            .WithDescription("章节版本管理")
            .WithTags("version")
            .RequireAuthorization();

        group.MapPost(string.Empty, async (
            string workId,
            string chapterId,
            CreateVersionRequest request,
            IChapterVersionManager mgr) =>
        {
            request.ChapterId = chapterId;
            return await mgr.CreateVersionAsync(request);
        }).WithName("create_version");

        group.MapGet(string.Empty, async (
            string workId,
            string chapterId,
            IChapterVersionManager mgr,
            CancellationToken cancellationToken) =>
        {
            return await mgr.ListVersionsAsync(workId, chapterId, cancellationToken);
        }).WithName("list_versions");

        group.MapGet("{versionId}", async (
            string workId,
            string chapterId,
            string versionId,
            IChapterVersionManager mgr) =>
        {
            return await mgr.GetVersionAsync(versionId);
        }).WithName("get_version");

        group.MapPost("{versionId}/rollback", async (
            string workId,
            string chapterId,
            string versionId,
            IChapterVersionManager mgr) =>
        {
            return await mgr.RollbackToVersionAsync(chapterId, versionId);
        }).WithName("rollback_version");

        group.MapPost("{versionId}/merge", async (
            string workId,
            string chapterId,
            string versionId,
            IChapterVersionManager mgr) =>
        {
            return await mgr.MergeFromVersionAsync(chapterId, versionId);
        }).WithName("merge_version");

        group.MapDelete("{versionId}", async (
            string workId,
            string chapterId,
            string versionId,
            IChapterVersionManager mgr) =>
        {
            return await mgr.DeleteVersionAsync(versionId);
        }).WithName("delete_version");

        group.MapPost("{versionId}/save-as-chapter", async (
            string workId,
            string chapterId,
            string versionId,
            SaveAsNewChapterRequest request,
            IChapterVersionManager mgr) =>
        {
            request.ChapterId = chapterId;
            request.SourceVersionId = versionId;
            return await mgr.SaveAsNewChapterAsync(request);
        }).WithName("save_version_as_chapter");
    }
}
