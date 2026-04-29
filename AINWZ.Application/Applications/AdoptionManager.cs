using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Application.Contracts.Version;
using SpeakEase.Write.Application.Contracts.Version.Dto;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

public sealed class AdoptionManager : IAdoptionManager
{
    private readonly SpeakEaseDbContext _db;
    private readonly IUserContext _user;
    private readonly IChapterVersionManager _versionMgr;
    private readonly ICreationSessionManager _sessionMgr;
    private readonly ILogger<AdoptionManager> _log;

    public AdoptionManager(
        SpeakEaseDbContext db,
        IUserContext user,
        IChapterVersionManager versionMgr,
        ICreationSessionManager sessionMgr,
        ILogger<AdoptionManager> log)
    {
        _db = db;
        _user = user;
        _versionMgr = versionMgr;
        _sessionMgr = sessionMgr;
        _log = log;
    }

    public async Task<ApiResult<ChapterDetailResponse>> AdoptFullAsync(AdoptChapterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkId))
            return new ApiResult<ChapterDetailResponse>("作品ID不能为空", 400);
        if (string.IsNullOrWhiteSpace(request.ChapterId))
            return new ApiResult<ChapterDetailResponse>("章节ID不能为空", 400);
        if (string.IsNullOrWhiteSpace(request.Content))
            return new ApiResult<ChapterDetailResponse>("采纳内容不能为空", 400);

        var work = await _db.Works.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WorkId && w.UserId == _user.UserId);
        if (work == null)
            return new ApiResult<ChapterDetailResponse>("作品不存在或无权访问", 404);

        var chapter = await _db.Chapters
            .FirstOrDefaultAsync(c => c.Id == request.ChapterId && c.WorkId == request.WorkId);
        if (chapter == null)
            return new ApiResult<ChapterDetailResponse>("章节不存在", 404);

        var versionResult = await _versionMgr.CreateVersionAsync(new CreateVersionRequest
        {
            ChapterId = request.ChapterId,
            Content = request.Content,
            Summary = request.Summary,
            Source = "ai-generate"
        });

        chapter.Content = request.Content;
        chapter.Summary = request.Summary;
        chapter.WordCount = CountWords(request.Content);
        chapter.LastContentSavedAt = DateTime.UtcNow;
        chapter.UpdateBy = _user.UserId;
        chapter.UpdateAt = DateTime.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            await _db.SaveChangesAsync();

            var totalWords = await _db.Chapters.AsNoTracking()
                .Where(c => c.WorkId == request.WorkId)
                .SumAsync(c => c.WordCount);

            await _db.Works
                .Where(w => w.Id == request.WorkId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.TotalWordCount, totalWords)
                    .SetProperty(x => x.UpdateAt, DateTime.UtcNow));

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _log.LogError(ex, "采纳内容到章节 {ChapterId} 事务失败，已回滚", request.ChapterId);
            return new ApiResult<ChapterDetailResponse>("采纳失败，请稍后重试", 500);
        }

        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            await _sessionMgr.AdoptContentAsync(request.SessionId, new AdoptContentRequest
            {
                Content = request.Content,
                Summary = request.Summary
            });
        }

        _log.LogInformation("用户 {UserId} 将AI生成内容采纳到章节 {ChapterId}，版本 {Version}",
            _user.UserId, request.ChapterId, versionResult.Data?.VersionNumber);

        return new ApiResult<ChapterDetailResponse>(new ChapterDetailResponse
        {
            Id = chapter.Id,
            WorkId = chapter.WorkId,
            Title = chapter.Title,
            Sequence = chapter.Sequence,
            WordCount = chapter.WordCount,
            Status = chapter.Status,
            Summary = chapter.Summary,
            AuthorNotes = chapter.AuthorNotes,
            LastContentSavedAt = chapter.LastContentSavedAt,
            Content = chapter.Content
        });
    }

    public async Task<ApiResult> DiscardAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return new ApiResult("会话ID不能为空", 400);

        await _sessionMgr.CancelSessionAsync(sessionId);
        _log.LogInformation("用户 {UserId} 放弃会话 {SessionId} 的生成内容", _user.UserId, sessionId);

        return new ApiResult(true);
    }

    private static int CountWords(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return 0;
        return content.Count(c => !char.IsWhiteSpace(c));
    }
}
