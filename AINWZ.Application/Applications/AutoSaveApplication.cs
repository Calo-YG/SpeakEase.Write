using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Infrastructure.AI.Memory;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

public sealed class AutoSaveApplication(
    SpeakEaseDbContext db,
    IUserContext user,
    IMemoryProvider memory,
    ILogger<AutoSaveApplication> logger) : IAutoSaveApplication
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "chapter", "character", "worldsetting", "outline", "inspiration"
    };

    public async Task<ApiResult> AutoSaveAsync(AutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.EntityType))
            return new ApiResult("实体类型不能为空", 400);
        if (string.IsNullOrWhiteSpace(request.EntityId))
            return new ApiResult("实体ID不能为空", 400);
        if (!SupportedTypes.Contains(request.EntityType))
            return new ApiResult($"不支持自动保存的实体类型: {request.EntityType}", 400);

        var userId = user.UserId;
        var now = DateTime.UtcNow;

        switch (request.EntityType.ToLowerInvariant())
        {
            case "chapter":
                return await AutoSaveChapter(request.EntityId, request, userId, now, cancellationToken);
            case "character":
                return await AutoSaveCharacter(request.EntityId, request, userId, now, cancellationToken);
            case "worldsetting":
                return await AutoSaveWorldSetting(request.EntityId, request, userId, now, cancellationToken);
            case "outline":
                return await AutoSaveOutline(request.EntityId, request, userId, now, cancellationToken);
            case "inspiration":
                return await AutoSaveInspiration(request.EntityId, request, userId, now, cancellationToken);
            default:
                return new ApiResult($"未知实体类型: {request.EntityType}", 400);
        }
    }

    private async Task<ApiResult> AutoSaveChapter(string chapterId, AutoSaveRequest req, string userId, DateTime now, CancellationToken ct)
    {
        var entity = await db.Chapters.FirstOrDefaultAsync(c => c.Id == chapterId && c.OwnerId == userId, ct);
        if (entity is null) return new ApiResult("章节不存在", 404);

        var changed = false;

        if (req.Content is not null && req.Content != entity.Content)
        {
            entity.Content = req.Content;
            entity.WordCount = req.Content.Count(c => !char.IsWhiteSpace(c));
            entity.LastContentSavedAt = now;
            changed = true;
        }
        if (req.Title is not null && req.Title != entity.Title)
        {
            entity.Title = req.Title.Trim();
            changed = true;
        }
        if (req.Summary is not null && req.Summary != entity.Summary)
        {
            entity.Summary = req.Summary;
            changed = true;
        }

        if (!changed) return new ApiResult(true);

        entity.UpdateBy = userId;
        entity.UpdateAt = now;
        await db.SaveChangesAsync(ct);

        memory.Invalidate(userId, entity.WorkId);
        logger.LogDebug("自动保存章节 {ChapterId}，字数={Words}", chapterId, entity.WordCount);

        return new ApiResult(true);
    }

    private async Task<ApiResult> AutoSaveCharacter(string characterId, AutoSaveRequest req, string userId, DateTime now, CancellationToken ct)
    {
        var entity = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.OwnerId == userId, ct);
        if (entity is null) return new ApiResult("角色不存在", 404);

        var changed = false;

        if (req.Content is not null && req.Content != entity.BackgroundStory)
        {
            entity.BackgroundStory = req.Content;
            changed = true;
        }
        if (req.Summary is not null && req.Summary != entity.Personality)
        {
            entity.Personality = req.Summary;
            changed = true;
        }
        if (req.Title is not null && req.Title != entity.Identity)
        {
            entity.Identity = req.Title.Trim();
            changed = true;
        }

        if (!changed) return new ApiResult(true);

        entity.UpdateBy = userId;
        entity.UpdateAt = now;
        await db.SaveChangesAsync(ct);

        memory.Invalidate(userId, entity.WorkId);
        logger.LogDebug("自动保存角色 {CharacterId} {Name}", characterId, entity.Name);

        return new ApiResult(true);
    }

    private async Task<ApiResult> AutoSaveWorldSetting(string worldSettingId, AutoSaveRequest req, string userId, DateTime now, CancellationToken ct)
    {
        var entity = await db.WorldSettings.FirstOrDefaultAsync(w => w.Id == worldSettingId && w.OwnerId == userId, ct);
        if (entity is null) return new ApiResult("世界观设定不存在", 404);

        var changed = false;

        if (req.Content is not null && req.Content != entity.JsonContent)
        {
            entity.JsonContent = req.Content;
            changed = true;
        }
        if (req.Summary is not null && req.Summary != entity.Summary)
        {
            entity.Summary = req.Summary;
            changed = true;
        }
        if (req.Title is not null && req.Title != entity.WorldName)
        {
            entity.WorldName = req.Title.Trim();
            changed = true;
        }

        if (!changed) return new ApiResult(true);

        entity.UpdateBy = userId;
        entity.UpdateAt = now;
        await db.SaveChangesAsync(ct);

        memory.Invalidate(userId, entity.WorkId);
        logger.LogDebug("自动保存世界观 {SettingId}", worldSettingId);

        return new ApiResult(true);
    }

    private async Task<ApiResult> AutoSaveOutline(string outlineNodeId, AutoSaveRequest req, string userId, DateTime now, CancellationToken ct)
    {
        var entity = await db.OutlineNodes.FirstOrDefaultAsync(o => o.Id == outlineNodeId && o.OwnerId == userId, ct);
        if (entity is null) return new ApiResult("大纲节点不存在", 404);

        var changed = false;

        if (req.Content is not null && req.Content != entity.Goal)
        {
            entity.Goal = req.Content;
            changed = true;
        }
        if (req.Title is not null && req.Title != entity.Title)
        {
            entity.Title = req.Title.Trim();
            changed = true;
        }

        if (!changed) return new ApiResult(true);

        entity.UpdateBy = userId;
        entity.UpdateAt = now;
        await db.SaveChangesAsync(ct);

        memory.Invalidate(userId, entity.WorkId);
        logger.LogDebug("自动保存大纲节点 {NodeId}", outlineNodeId);

        return new ApiResult(true);
    }

    private async Task<ApiResult> AutoSaveInspiration(string inspirationId, AutoSaveRequest req, string userId, DateTime now, CancellationToken ct)
    {
        var entity = await db.InspirationRecords.FirstOrDefaultAsync(i => i.Id == inspirationId && i.UserId == userId, ct);
        if (entity is null) return new ApiResult("灵感记录不存在", 404);

        var changed = false;

        if (req.Content is not null && req.Content != entity.Content)
        {
            entity.Content = req.Content;
            changed = true;
        }
        if (req.Title is not null && req.Title != entity.Title)
        {
            entity.Title = req.Title.Trim();
            changed = true;
        }

        if (!changed) return new ApiResult(true);

        entity.UpdateBy = userId;
        entity.UpdateAt = now;
        await db.SaveChangesAsync(ct);

        memory.Invalidate(userId, entity.WorkId);
        logger.LogDebug("自动保存灵感 {InspirationId}", inspirationId);

        return new ApiResult(true);
    }
}
