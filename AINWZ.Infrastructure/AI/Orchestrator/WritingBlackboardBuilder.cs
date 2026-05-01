using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class WritingBlackboardBuilder
{
    private readonly SpeakEaseDbContext _db;
    private readonly ILogger<WritingBlackboardBuilder> _logger;

    public WritingBlackboardBuilder(SpeakEaseDbContext db, ILogger<WritingBlackboardBuilder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<WritingBlackboard> BuildAsync(string workId, string requestId)
    {
        var board = new WritingBlackboard
        {
            WorkId = workId,
            RequestId = requestId
        };

        var work = await _db.Works.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == workId);

        if (work == null) return board;

        board.WorkTitle = work.Title ?? string.Empty;
        board.WorkSummary = work.Summary ?? string.Empty;

        board.Meta = new WritingMetaInfo
        {
            Genre = work.Genre ?? string.Empty,
            Perspective = work.Perspective ?? "third",
            StyleTags = work.StyleTags ?? new List<string>(),
            TotalWordCount = work.TotalWordCount
        };

        var chapters = await _db.Chapters.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderByDescending(x => x.Sequence)
            .Take(10)
            .ToListAsync();

        board.RecentChapters = chapters
            .OrderBy(c => c.Sequence)
            .Select(c => new ChapterSection
            {
                ChapterId = c.Id,
                Sequence = c.Sequence,
                Title = c.Title ?? string.Empty,
                Content = c.Content ?? string.Empty,
                Summary = c.Summary ?? string.Empty,
                WordCount = c.WordCount,
                Status = c.Status ?? "draft"
            })
            .ToList();

        var characters = await _db.Characters.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .Take(30)
            .ToListAsync();

        var characterIds = characters.Select(c => c.Id).ToHashSet();
        var relationships = await _db.CharacterRelationships.AsNoTracking()
            .Where(x => x.WorkId == workId
                && characterIds.Contains(x.SourceCharacterId))
            .ToListAsync();

        board.Characters = characters
            .Select(c =>
            {
                var rels = relationships
                    .Where(r => r.SourceCharacterId == c.Id)
                    .Select(r =>
                    {
                        var target = characters.FirstOrDefault(tc => tc.Id == r.TargetCharacterId);
                        return $"{target?.Name ?? r.TargetCharacterId}({r.RelationshipType}): {r.Description}";
                    })
                    .ToList();

                var fears = new List<string>();
                var desires = new List<string>();
                if (c.Metadata != null)
                {
                    if (c.Metadata.TryGetValue("fears", out var f) && !string.IsNullOrEmpty(f))
                        fears = f.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                    if (c.Metadata.TryGetValue("desires", out var d) && !string.IsNullOrEmpty(d))
                        desires = d.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                }

                return new CharacterSection
                {
                    CharacterId = c.Id,
                    Name = c.Name ?? string.Empty,
                    CoreSeed = c.Identity ?? string.Empty,
                    Background = c.BackgroundStory ?? string.Empty,
                    Personality = c.Personality ?? string.Empty,
                    Traits = c.Gender ?? string.Empty,
                    Voice = c.Alias ?? string.Empty,
                    Arc = c.Motivation ?? string.Empty,
                    Relationships = rels,
                    Fears = fears,
                    Desires = desires
                };
            })
            .ToList();

        var worldSetting = await _db.WorldSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId);

        if (worldSetting != null)
        {
            var wsSection = new WorldSettingSection();

            if (!string.IsNullOrEmpty(worldSetting.JsonContent))
            {
                try
                {
                    using var doc = JsonDocument.Parse(worldSetting.JsonContent);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("worldRules", out var wr)) wsSection.WorldRules = wr.GetString() ?? string.Empty;
                    if (root.TryGetProperty("geography", out var geo)) wsSection.Geography = geo.GetString() ?? string.Empty;
                    if (root.TryGetProperty("factions", out var fac)) wsSection.Factions = fac.GetString() ?? string.Empty;
                    if (root.TryGetProperty("history", out var his)) wsSection.History = his.GetString() ?? string.Empty;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "世界观设定 JSON 解析失败: WorkId={WorkId}", workId);
                }
            }

            if (string.IsNullOrEmpty(wsSection.WorldRules) && !string.IsNullOrEmpty(worldSetting.Summary))
                wsSection.WorldRules = worldSetting.Summary;

            wsSection.LastUpdatedAt = worldSetting.UpdateAt;
            board.WorldSetting = wsSection;
        }

        var volumes = await _db.Volumes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Sequence)
            .ToListAsync();

        var outlineNodes = await _db.OutlineNodes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Sequence)
            .Take(80)
            .ToListAsync();

        board.Outline = new OutlineSection
        {
            Volumes = volumes.Select(v => new VolumeNode
            {
                Sequence = v.Sequence,
                Title = v.Title ?? string.Empty,
                Summary = v.Summary ?? string.Empty,
                Chapters = chapters
                    .Where(c => c.VolumeId == v.Id)
                    .OrderBy(c => c.Sequence)
                    .Select(c => new ChapterNode
                    {
                        Sequence = c.Sequence,
                        Title = c.Title ?? string.Empty,
                        Summary = c.Summary ?? string.Empty,
                        Status = c.Status ?? "draft"
                    })
                    .ToList()
            }).ToList(),
            OutlineNodes = outlineNodes.Select(n => new OutlineNodeSection
            {
                Id = n.Id,
                ParentId = n.ParentNodeId ?? string.Empty,
                Title = n.Title ?? string.Empty,
                Goal = n.Goal ?? string.Empty,
                KeyEvent = n.KeyEvent ?? string.Empty,
                Sequence = n.Sequence,
                StageType = n.StageType ?? string.Empty,
                NodeChapterId = string.Empty
            }).ToList(),
            LastUpdatedAt = DateTime.UtcNow
        };

        var foreshadowings = await _db.Foreshadowings.AsNoTracking()
            .Where(x => x.WorkId == workId && (x.Status == "pending" || x.Status == "active"))
            .Take(30)
            .ToListAsync();

        board.AuditResults = foreshadowings
            .Select(f => new AuditResultSection
            {
                CheckType = "foreshadowing",
                Severity = f.Importance >= 8 ? "high" : f.Importance >= 5 ? "medium" : "low",
                Description = f.Description ?? f.Title ?? string.Empty,
                Suggestion = $"伏笔「{f.Title}」状态: {f.Status}, 埋设章节: {f.SetupChapterId}, 回收章节: {f.PayoffChapterId}"
            })
            .ToList();

        return board;
    }
}
