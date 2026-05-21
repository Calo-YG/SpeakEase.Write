using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Application.Contracts.Version;
using SpeakEase.Write.Application.Contracts.Version.Dto;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

public sealed class ChapterVersionManager : IChapterVersionManager
{
    private const int MaxVersionsPerChapter = 20;

    private readonly SpeakEaseDbContext _db;
    private readonly ISnowflakeIdGenerator _idGen;
    private readonly IUserContext _user;
    private readonly ILogger<ChapterVersionManager> _log;

    public ChapterVersionManager(
        SpeakEaseDbContext db,
        ISnowflakeIdGenerator idGen,
        IUserContext user,
        ILogger<ChapterVersionManager> log)
    {
        _db = db;
        _idGen = idGen;
        _user = user;
        _log = log;
    }

    public async Task<ApiResult<ChapterVersionDto>> CreateVersionAsync(CreateVersionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ChapterId))
            return new ApiResult<ChapterVersionDto>("章节标识不能为空", 400);

        if (!IsValidSource(request.Source))
            return new ApiResult<ChapterVersionDto>("无效的版本来源", 400);

        var chapter = await _db.Chapters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChapterId);
        if (chapter == null)
            return new ApiResult<ChapterVersionDto>("章节不存在", 404);

        var work = await _db.Works.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == chapter.WorkId && w.UserId == _user.UserId);
        if (work == null)
            return new ApiResult<ChapterVersionDto>("无权操作此章节", 403);

        var maxVersion = await _db.ChapterVersions
            .Where(v => v.ChapterId == request.ChapterId)
            .MaxAsync(v => (int?)v.VersionNumber) ?? 0;

        var entity = new ChapterVersionEntity
        {
            Id = _idGen.NextIdString(),
            ChapterId = request.ChapterId,
            OwnerId = _user.UserId,
            VersionNumber = maxVersion + 1,
            Content = request.Content ?? string.Empty,
            Summary = request.Summary ?? string.Empty,
            Source = request.Source,
            ModelId = request.ModelId ?? string.Empty,
            CreateBy = _user.UserId,
            UpdateBy = _user.UserId
        };

        _db.ChapterVersions.Add(entity);
        await EnforceRetentionPolicyAsync(request.ChapterId);
        await _db.SaveChangesAsync();

        _log.LogInformation("章节 {ChapterId} 创建版本 {Version}（来源：{Source}）",
            request.ChapterId, entity.VersionNumber, request.Source);

        return new ApiResult<ChapterVersionDto>(MapToSummaryDto(entity));
    }

    public async Task<ApiResult<List<ChapterVersionDto>>> ListVersionsAsync(string chapterId)
    {
        var versions = await _db.ChapterVersions
            .AsNoTracking()
            .Where(v => v.ChapterId == chapterId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ChapterVersionDto
            {
                VersionId = v.Id,
                ChapterId = v.ChapterId,
                VersionNumber = v.VersionNumber,
                Summary = v.Summary,
                Source = v.Source,
                ModelId = v.ModelId,
                CreatedAt = v.CreateAt
            })
            .ToListAsync();

        return new ApiResult<List<ChapterVersionDto>>(versions);
    }

    public async Task<ApiResult<ChapterVersionDetailDto>> GetVersionAsync(string versionId)
    {
        var version = await _db.ChapterVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.OwnerId == _user.UserId);

        if (version == null)
            return new ApiResult<ChapterVersionDetailDto>("版本不存在或无权访问", 404);

        return new ApiResult<ChapterVersionDetailDto>(MapToDetailDto(version));
    }

    public async Task<ApiResult<ChapterVersionDto>> RollbackToVersionAsync(string chapterId, string targetVersionId)
    {
        var chapter = await _db.Chapters
            .FirstOrDefaultAsync(c => c.Id == chapterId);
        if (chapter == null)
            return new ApiResult<ChapterVersionDto>("章节不存在", 404);

        var work = await _db.Works.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == chapter.WorkId && w.UserId == _user.UserId);
        if (work == null)
            return new ApiResult<ChapterVersionDto>("无权操作此章节", 403);

        var target = await _db.ChapterVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == targetVersionId && v.ChapterId == chapterId);
        if (target == null)
            return new ApiResult<ChapterVersionDto>("目标版本不存在", 404);

        chapter.Content = target.Content;
        chapter.Summary = target.Summary;
        chapter.UpdateAt = DateTime.Now;
        chapter.UpdateBy = _user.UserId;

        var maxVersion = await _db.ChapterVersions
            .Where(v => v.ChapterId == chapterId)
            .MaxAsync(v => (int?)v.VersionNumber) ?? 0;

        var rollbackEntity = new ChapterVersionEntity
        {
            Id = _idGen.NextIdString(),
            ChapterId = chapterId,
            OwnerId = _user.UserId,
            VersionNumber = maxVersion + 1,
            Content = target.Content,
            Summary = $"回滚至版本 {target.VersionNumber}: {target.Summary}",
            Source = "rollback",
            ModelId = string.Empty,
            CreateBy = _user.UserId,
            UpdateBy = _user.UserId
        };

        _db.ChapterVersions.Add(rollbackEntity);
        await EnforceRetentionPolicyAsync(chapterId);
        await _db.SaveChangesAsync();

        _log.LogInformation("章节 {ChapterId} 回滚至版本 {Version}", chapterId, target.VersionNumber);

        return new ApiResult<ChapterVersionDto>(MapToSummaryDto(rollbackEntity));
    }

    public async Task<ApiResult<ChapterVersionDto>> MergeFromVersionAsync(string chapterId, string sourceVersionId)
    {
        var chapter = await _db.Chapters
            .FirstOrDefaultAsync(c => c.Id == chapterId);
        if (chapter == null)
            return new ApiResult<ChapterVersionDto>("章节不存在", 404);

        var work = await _db.Works.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == chapter.WorkId && w.UserId == _user.UserId);
        if (work == null)
            return new ApiResult<ChapterVersionDto>("无权操作此章节", 403);

        var source = await _db.ChapterVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == sourceVersionId && v.ChapterId == chapterId);
        if (source == null)
            return new ApiResult<ChapterVersionDto>("源版本不存在", 404);

        var mergedContent = $"[Merge: 当前内容 + 版本 {source.VersionNumber} 内容]\n--- 当前内容 ---\n{chapter.Content}\n--- 合并内容 ---\n{source.Content}";
        var mergedSummary = $"合并版本 {source.VersionNumber}: {source.Summary}";

        var maxVersion = await _db.ChapterVersions
            .Where(v => v.ChapterId == chapterId)
            .MaxAsync(v => (int?)v.VersionNumber) ?? 0;

        var mergeEntity = new ChapterVersionEntity
        {
            Id = _idGen.NextIdString(),
            ChapterId = chapterId,
            OwnerId = _user.UserId,
            VersionNumber = maxVersion + 1,
            Content = mergedContent,
            Summary = mergedSummary,
            Source = "merge",
            ModelId = string.Empty,
            CreateBy = _user.UserId,
            UpdateBy = _user.UserId
        };

        chapter.Content = mergedContent;
        chapter.Summary = mergedSummary;
        chapter.UpdateAt = DateTime.Now;
        chapter.UpdateBy = _user.UserId;

        _db.ChapterVersions.Add(mergeEntity);
        await EnforceRetentionPolicyAsync(chapterId);
        await _db.SaveChangesAsync();

        _log.LogInformation("章节 {ChapterId} 合并版本 {Version}", chapterId, source.VersionNumber);

        return new ApiResult<ChapterVersionDto>(MapToSummaryDto(mergeEntity));
    }

    public async Task<ApiResult> DeleteVersionAsync(string versionId)
    {
        var version = await _db.ChapterVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.OwnerId == _user.UserId);

        if (version == null)
            return new ApiResult("版本不存在或无权访问", 404);

        _db.ChapterVersions.Remove(version);
        await _db.SaveChangesAsync();

        _log.LogInformation("版本 {VersionId} 已删除", versionId);

        return new ApiResult(true);
    }

    public async Task<ApiResult<ChapterItemResponse>> SaveAsNewChapterAsync(SaveAsNewChapterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ChapterId))
            return new ApiResult<ChapterItemResponse>("原章节ID不能为空", 400);
        if (string.IsNullOrWhiteSpace(request.SourceVersionId))
            return new ApiResult<ChapterItemResponse>("源版本ID不能为空", 400);

        var work = await _db.Works.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == _db.Chapters
                .Where(c => c.Id == request.ChapterId)
                .Select(c => c.WorkId)
                .FirstOrDefault() && w.UserId == _user.UserId);
        if (work == null)
            return new ApiResult<ChapterItemResponse>("无权操作此作品", 403);

        var sourceVersion = await _db.ChapterVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.SourceVersionId && v.ChapterId == request.ChapterId);
        if (sourceVersion == null)
            return new ApiResult<ChapterItemResponse>("源版本不存在", 404);

        var maxSeq = await _db.Chapters.AsNoTracking()
            .Where(c => c.WorkId == work.Id)
            .Select(c => (int?)c.Sequence)
            .MaxAsync() ?? 0;

        var newChapter = new ChapterEntity
        {
            Id = _idGen.NextIdString(),
            WorkId = work.Id,
            OwnerId = _user.UserId,
            Title = string.IsNullOrWhiteSpace(request.NewTitle)
                ? SafeTruncate($"（来自版本 {sourceVersion.VersionNumber}）{sourceVersion.Summary}", 200)
                : request.NewTitle.Trim(),
            Sequence = maxSeq + 1,
            Content = sourceVersion.Content,
            Summary = sourceVersion.Summary,
            WordCount = CountWords(sourceVersion.Content),
            Status = "draft",
            CreateBy = _user.UserId,
            UpdateBy = _user.UserId
        };

        _db.Chapters.Add(newChapter);
        await _db.SaveChangesAsync();

        _log.LogInformation("版本 {VersionId} 另存为新章节 {ChapterId}: {Title}",
            request.SourceVersionId, newChapter.Id, newChapter.Title);

        return new ApiResult<ChapterItemResponse>(new ChapterItemResponse
        {
            Id = newChapter.Id,
            WorkId = newChapter.WorkId,
            Title = newChapter.Title,
            Sequence = newChapter.Sequence,
            WordCount = newChapter.WordCount,
            Status = newChapter.Status,
            Summary = newChapter.Summary
        });
    }

    private static bool IsValidSource(string source)
        => source is "manual" or "autosave" or "ai-generate" or "rollback" or "merge";

    private static ChapterVersionDto MapToSummaryDto(ChapterVersionEntity entity)
        => new ChapterVersionDto
        {
            VersionId = entity.Id,
            ChapterId = entity.ChapterId,
            VersionNumber = entity.VersionNumber,
            Summary = entity.Summary,
            Source = entity.Source,
            ModelId = entity.ModelId,
            CreatedAt = entity.CreateAt
        };

    private static ChapterVersionDetailDto MapToDetailDto(ChapterVersionEntity entity)
        => new ChapterVersionDetailDto
        {
            VersionId = entity.Id,
            ChapterId = entity.ChapterId,
            VersionNumber = entity.VersionNumber,
            Content = entity.Content,
            Summary = entity.Summary,
            Source = entity.Source,
            ModelId = entity.ModelId,
            CreatedAt = entity.CreateAt
        };

    private async Task EnforceRetentionPolicyAsync(string chapterId)
    {
        var count = await _db.ChapterVersions
            .CountAsync(v => v.ChapterId == chapterId);

        if (count <= MaxVersionsPerChapter) return;

        var excess = count - MaxVersionsPerChapter;
        var oldestIds = await _db.ChapterVersions
            .Where(v => v.ChapterId == chapterId)
            .OrderBy(v => v.VersionNumber)
            .Take(excess)
            .Select(v => v.Id)
            .ToListAsync();

        await _db.ChapterVersions
            .Where(v => oldestIds.Contains(v.Id))
            .ExecuteDeleteAsync();

        _log.LogDebug("章节 {ChapterId} 已清理 {Count} 个旧版本", chapterId, excess);
    }

    private static string SafeTruncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text ?? string.Empty;
        return text[..maxLen];
    }

    private static int CountWords(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return 0;
        return content.Count(c => !char.IsWhiteSpace(c));
    }
}
