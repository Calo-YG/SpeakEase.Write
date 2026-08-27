using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Application.Contracts.Version;
using SpeakEase.Write.Application.Contracts.Version.Dto;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEaseDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Applications;

// 内容采纳管理器：负责将AI生成的内容正式采纳到作品章节，含版本管理、字数统计和事务保护
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

    // 完整采纳：将AI生成内容写入章节、创建版本快照，并在事务中同步更新作品总字数
    public async Task<ApiResult<ChapterDetailResponse>> AdoptFullAsync(AdoptChapterRequest request)
    {
        // 参数校验：WorkId、ChapterId、Content 均不能为空
        if (string.IsNullOrWhiteSpace(request.WorkId))
            return new ApiResult<ChapterDetailResponse>("作品ID不能为空", 400);
        if (string.IsNullOrWhiteSpace(request.ChapterId))
            return new ApiResult<ChapterDetailResponse>("章节ID不能为空", 400);
        if (string.IsNullOrWhiteSpace(request.Content))
            return new ApiResult<ChapterDetailResponse>("采纳内容不能为空", 400);

        // 验证作品归属：只有作品作者才能操作
        var work = await _db.Works.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WorkId && w.UserId == _user.UserId);
        if (work == null)
            return new ApiResult<ChapterDetailResponse>("作品不存在或无权访问", 404);

        var chapter = await _db.Chapters
            .FirstOrDefaultAsync(c => c.Id == request.ChapterId && c.WorkId == request.WorkId);
        if (chapter == null)
            return new ApiResult<ChapterDetailResponse>("章节不存在", 404);

        // 版本快照、章节内容和作品统计必须共享同一事务。
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 先创建版本快照（来源标记为 ai-generate）。版本管理器使用同一个 DbContext，
            // 因而其 SaveChanges 会参与当前事务。
            var versionResult = await _versionMgr.CreateVersionAsync(new CreateVersionRequest
            {
                ChapterId = request.ChapterId,
                Content = request.Content,
                Summary = request.Summary,
                Source = "ai-generate"
            });

            if (!versionResult.Successed || versionResult.Data == null)
            {
                await transaction.RollbackAsync();
                _log.LogWarning("为章节 {ChapterId} 创建采纳版本失败: {Message}",
                    request.ChapterId, versionResult.Message);
                return new ApiResult<ChapterDetailResponse>(
                    versionResult.Message ?? "版本创建失败，未采纳章节内容",
                    versionResult.Status > 0 ? versionResult.Status : 500);
            }

            // 更新章节内容、摘要和字数
            chapter.Content = request.Content;
            chapter.Summary = request.Summary;
            chapter.WordCount = CountWords(request.Content);
            chapter.LastContentSavedAt = DateTime.Now;
            chapter.UpdateBy = _user.UserId;
            chapter.UpdateAt = DateTime.Now;

            // 提交章节变更
            await _db.SaveChangesAsync();

            // 重新汇总作品下所有章节的字数（数据库层面SUM，不加载到内存）
            var totalWords = await _db.Chapters.AsNoTracking()
                .Where(c => c.WorkId == request.WorkId)
                .SumAsync(c => c.WordCount);

            // 使用ExecuteUpdateAsync批量更新作品总字数（IO-bound，不加载实体到内存）
            await _db.Works
                .Where(w => w.Id == request.WorkId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.TotalWordCount, totalWords)
                    .SetProperty(x => x.UpdateAt, DateTime.Now));

            await transaction.CommitAsync();
            _log.LogInformation("用户 {UserId} 将AI生成内容采纳到章节 {ChapterId}，版本 {Version}",
                _user.UserId, request.ChapterId, versionResult.Data.VersionNumber);
        }
        catch (Exception ex)
        {
            // 事务失败时回滚，保证章节和作品统计数据一致性
            await transaction.RollbackAsync();
            _log.LogError(ex, "采纳内容到章节 {ChapterId} 事务失败，已回滚", request.ChapterId);
            return new ApiResult<ChapterDetailResponse>("采纳失败，请稍后重试", 500);
        }

        // 如果提供了会话ID，标记该会话中的内容已被采纳
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            await _sessionMgr.AdoptContentAsync(request.SessionId, new AdoptContentRequest
            {
                Content = request.Content,
                Summary = request.Summary
            });
        }

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

    // 放弃AI生成内容：取消对应的创作会话
    public async Task<ApiResult> DiscardAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return new ApiResult("会话ID不能为空", 400);

        await _sessionMgr.CancelSessionAsync(sessionId);
        _log.LogInformation("用户 {UserId} 放弃会话 {SessionId} 的生成内容", _user.UserId, sessionId);

        return new ApiResult(true);
    }

    // 统计内容字数：按非空白字符计数
    private static int CountWords(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return 0;
        return content.Count(c => !char.IsWhiteSpace(c));
    }
}
