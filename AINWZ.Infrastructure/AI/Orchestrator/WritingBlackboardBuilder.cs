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

    public async Task<WritingBlackboard> BuildAsync(string workId, string requestId, ContextFocus? focus = null)
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

        var maxChapters = focus?.MaxChapters ?? 10;
        var chapters = await _db.Chapters.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderByDescending(x => x.Sequence)
            .Take(maxChapters)
            .ToListAsync();

        if (focus?.CurrentChapterId != null)
        {
            var currentChapter = await _db.Chapters.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == focus.CurrentChapterId && x.WorkId == workId);

            if (currentChapter != null && !chapters.Any(c => c.Id == currentChapter.Id))
            {
                var nearbyChapters = await _db.Chapters.AsNoTracking()
                    .Where(x => x.WorkId == workId && x.Sequence >= currentChapter.Sequence - 3
                                && x.Sequence <= currentChapter.Sequence + 1)
                    .OrderByDescending(x => x.Sequence)
                    .ToListAsync();

                chapters = nearbyChapters
                    .Concat(chapters)
                    .GroupBy(c => c.Id)
                    .Select(g => g.First())
                    .OrderByDescending(c => c.Sequence)
                    .Take(maxChapters)
                    .ToList();
            }
        }

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

        var maxCharacters = focus?.MaxCharacters ?? 30;
        var charactersQuery = _db.Characters.AsNoTracking()
            .Where(x => x.WorkId == workId);

        if (focus?.CharacterIds?.Count > 0)
        {
            var focusIds = focus.CharacterIds;
            var focusNames = focus.CharacterNames ?? new List<string>();

            var prioritized = await charactersQuery
                .Where(c => focusIds.Contains(c.Id)
                            || focusNames.Any(n => c.Name != null && c.Name.Contains(n)))
                .ToListAsync();

            var remaining = await charactersQuery
                .Where(c => !focusIds.Contains(c.Id)
                            && !focusNames.Any(n => c.Name != null && c.Name.Contains(n)))
                .Take(maxCharacters - prioritized.Count)
                .ToListAsync();

            var characters = prioritized.Concat(remaining).DistinctBy(c => c.Id).ToList();

            await LoadCharactersIntoBoard(board, characters, workId);
        }
        else
        {
            var characters = await charactersQuery.Take(maxCharacters).ToListAsync();
            await LoadCharactersIntoBoard(board, characters, workId);
        }

        var characterIds = board.Characters.Select(c => c.CharacterId).ToHashSet();

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
            .Where(x => x.WorkId == workId && (x.Status == "pending" || x.Status == "active" || x.Status == "hinted"))
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

        var chapterIdToSequence = chapters.ToDictionary(c => c.Id, c => c.Sequence);

        board.Foreshadowings = new ForeshadowingBlackboardSection
        {
            Pending = foreshadowings
                .Where(f => f.Status == "pending")
                .Select(f => new ForeshadowingEntry
                {
                    Id = f.Id,
                    Title = f.Title ?? string.Empty,
                    Description = f.Description ?? string.Empty,
                    Importance = f.Importance,
                    SetupChapterId = f.SetupChapterId ?? string.Empty,
                    SetupChapterSequence = chapterIdToSequence.GetValueOrDefault(f.SetupChapterId ?? string.Empty),
                    PayoffChapterId = f.PayoffChapterId ?? string.Empty,
                    Status = f.Status
                })
                .OrderByDescending(f => f.Importance)
                .ToList(),
            Hinted = foreshadowings
                .Where(f => f.Status == "hinted")
                .Select(f => new ForeshadowingEntry
                {
                    Id = f.Id,
                    Title = f.Title ?? string.Empty,
                    Description = f.Description ?? string.Empty,
                    Importance = f.Importance,
                    SetupChapterId = f.SetupChapterId ?? string.Empty,
                    SetupChapterSequence = chapterIdToSequence.GetValueOrDefault(f.SetupChapterId ?? string.Empty),
                    PayoffChapterId = f.PayoffChapterId ?? string.Empty,
                    Status = f.Status
                })
                .OrderByDescending(f => f.Importance)
                .ToList()
        };

        var currentSequence = chapters.Count > 0 ? chapters.Max(c => c.Sequence) : 0;
        board.Foreshadowings.OverdueCount = board.Foreshadowings.Pending
            .Count(f => f.SetupChapterSequence > 0 && currentSequence - f.SetupChapterSequence > 5);

        var timelineEvents = await _db.TimelineEvents.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.EventTime)
            .Take(30)
            .ToListAsync();

        board.TimelineEvents = timelineEvents
            .Select(t => new TimelineEventSection
            {
                Id = t.Id,
                Title = t.Title ?? string.Empty,
                Description = t.Description ?? string.Empty,
                EventTime = t.EventTime,
                EventType = t.EventType ?? string.Empty,
                ChapterId = t.ChapterId ?? string.Empty,
                ChapterSequence = chapterIdToSequence.GetValueOrDefault(t.ChapterId ?? string.Empty),
                RelatedCharacterIds = t.RelatedCharacterIds ?? new List<string>()
            })
            .ToList();

        return board;
    }

    private async Task LoadCharactersIntoBoard(
        WritingBlackboard board,
        List<Domain.Entities.Story.CharacterEntity> characters,
        string workId)
    {
        var characterIds = characters.Select(c => c.Id).ToHashSet();
        var relationships = await _db.CharacterRelationships.AsNoTracking()
            .Where(x => x.WorkId == workId && characterIds.Contains(x.SourceCharacterId))
            .ToListAsync();

        board.Characters = characters.Select(c =>
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
        }).ToList();
    }
}
