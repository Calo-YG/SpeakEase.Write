using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Analysis;

public interface IForeshadowAnalysisService
{
    Task<ForeshadowAnalysisResult> AnalyzeChapterAsync(string workId, string chapterId, CancellationToken ct = default);
    Task<List<OverdueForeshadowing>> GetOverdueForeshadowingsAsync(string workId, CancellationToken ct = default);
}

public sealed class ForeshadowAnalysisService : IForeshadowAnalysisService
{
    private readonly SpeakEaseDbContext _db;
    private readonly ILogger<ForeshadowAnalysisService> _logger;

    public ForeshadowAnalysisService(SpeakEaseDbContext db, ILogger<ForeshadowAnalysisService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ForeshadowAnalysisResult> AnalyzeChapterAsync(string workId, string chapterId, CancellationToken ct = default)
    {
        var chapter = await _db.Chapters.AsNoTracking()
            .Where(c => c.Id == chapterId && c.WorkId == workId)
            .Select(c => new { c.Id, c.Sequence, c.Title, c.Content, c.Summary })
            .FirstOrDefaultAsync(ct);

        if (chapter == null)
            return new ForeshadowAnalysisResult();

        var pendingForeshadowings = await _db.Foreshadowings.AsNoTracking()
            .Where(f => f.WorkId == workId && (f.Status == "pending" || f.Status == "hinted"))
            .Select(f => new { f.Id, f.Title, f.Description, f.Importance, f.SetupChapterId, f.Status })
            .ToListAsync(ct);

        if (pendingForeshadowings.Count == 0)
            return new ForeshadowAnalysisResult();

        var chapterIdToSeq = await _db.Chapters.AsNoTracking()
            .Where(c => c.WorkId == workId)
            .ToDictionaryAsync(c => c.Id, c => c.Sequence, ct);

        var chapterText = $"{chapter.Title} {chapter.Content} {chapter.Summary}";

        var result = new ForeshadowAnalysisResult();

        foreach (var f in pendingForeshadowings)
        {
            var setupSeq = chapterIdToSeq.GetValueOrDefault(f.SetupChapterId ?? string.Empty);
            var chaptersSinceSetup = chapter.Sequence - setupSeq;
            var foreshadowRef = new ForeshadowingReference
            {
                ForeshadowingId = f.Id,
                Title = f.Title,
                Status = f.Status,
                Importance = f.Importance,
                ChaptersSinceSetup = chaptersSinceSetup
            };

            if (ContainsForeshadowingKeywords(chapterText, f.Title, f.Description))
            {
                foreshadowRef.IsReferenced = true;
                foreshadowRef.Confidence = CalculateConfidence(chapterText, f.Title, f.Description);
                result.ReferencedForeshadowings.Add(foreshadowRef);
            }

            if (chaptersSinceSetup > 5 && f.Importance >= 5)
            {
                foreshadowRef.IsOverdue = true;
                result.OverdueForeshadowings.Add(foreshadowRef);
            }
            else if (chaptersSinceSetup > 3 && f.Importance >= 7)
            {
                foreshadowRef.IsOverdue = true;
                result.OverdueForeshadowings.Add(foreshadowRef);
            }
        }

        result.OverdueForeshadowings = result.OverdueForeshadowings
            .OrderByDescending(f => f.Importance)
            .ThenByDescending(f => f.ChaptersSinceSetup)
            .ToList();

        result.ReferencedForeshadowings = result.ReferencedForeshadowings
            .OrderByDescending(f => f.Confidence)
            .ToList();

        _logger.LogDebug("章节 {ChapterId} 伏笔分析：引用 {Referenced}，逾期 {Overdue}",
            chapterId, result.ReferencedForeshadowings.Count, result.OverdueForeshadowings.Count);

        return result;
    }

    public async Task<List<OverdueForeshadowing>> GetOverdueForeshadowingsAsync(string workId, CancellationToken ct = default)
    {
        var chapters = await _db.Chapters.AsNoTracking()
            .Where(c => c.WorkId == workId)
            .Select(c => new { c.Id, c.Sequence })
            .ToListAsync(ct);

        if (chapters.Count == 0)
            return new List<OverdueForeshadowing>();

        var chapterIdToSeq = chapters.ToDictionary(c => c.Id, c => c.Sequence);
        var maxSequence = chapters.Max(c => c.Sequence);

        var pending = await _db.Foreshadowings.AsNoTracking()
            .Where(f => f.WorkId == workId && (f.Status == "pending" || f.Status == "hinted"))
            .ToListAsync(ct);

        var result = new List<OverdueForeshadowing>();

        foreach (var f in pending)
        {
            var setupSeq = chapterIdToSeq.GetValueOrDefault(f.SetupChapterId ?? string.Empty);
            var age = setupSeq > 0 ? maxSequence - setupSeq : 0;

            var threshold = f.Importance >= 7 ? 3 : f.Importance >= 5 ? 5 : 8;
            if (age > threshold)
            {
                result.Add(new OverdueForeshadowing
                {
                    ForeshadowingId = f.Id,
                    Title = f.Title ?? string.Empty,
                    Description = f.Description ?? string.Empty,
                    Importance = f.Importance,
                    ChaptersSinceSetup = age,
                    Status = f.Status ?? string.Empty,
                    Urgency = age > threshold * 2 ? "critical" : "warning"
                });
            }
        }

        return result.OrderByDescending(f => f.Urgency == "critical" ? 1 : 0)
            .ThenByDescending(f => f.Importance)
            .ToList();
    }

    private static bool ContainsForeshadowingKeywords(string chapterText, string title, string description)
    {
        if (string.IsNullOrEmpty(chapterText) || string.IsNullOrEmpty(title))
            return false;

        var titleKeywords = ExtractKeywords(title);
        var descKeywords = ExtractKeywords(description ?? string.Empty);
        var allKeywords = titleKeywords.Concat(descKeywords).Distinct().ToList();

        if (allKeywords.Count == 0)
            return false;

        var matchCount = allKeywords.Count(kw => chapterText.Contains(kw, StringComparison.OrdinalIgnoreCase));
        return matchCount >= Math.Max(1, allKeywords.Count / 3);
    }

    private static List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var separators = new[]
        {
            ' ', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')',
            '\n', '\r', '\t', '\u00AB', '\u00BB',
            '\u2018', '\u2019', '\u201C', '\u201D',
            '\u3001', '\u3002', '\uFF01', '\uFF1F', '\uFF0C', '\uFF1B', '\uFF1A',
            '\uFF08', '\uFF09', '\u300A', '\u300B'
        };
        return text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2)
            .Distinct()
            .ToList();
    }

    private static double CalculateConfidence(string chapterText, string title, string description)
    {
        var titleKeywords = ExtractKeywords(title);
        var descKeywords = ExtractKeywords(description ?? string.Empty);
        var allKeywords = titleKeywords.Concat(descKeywords).Distinct().ToList();

        if (allKeywords.Count == 0) return 0;

        var matchCount = allKeywords.Count(kw => chapterText.Contains(kw, StringComparison.OrdinalIgnoreCase));
        return Math.Min(1.0, (double)matchCount / allKeywords.Count);
    }
}

public sealed class ForeshadowAnalysisResult
{
    public List<ForeshadowingReference> ReferencedForeshadowings { get; set; } = new();
    public List<ForeshadowingReference> OverdueForeshadowings { get; set; } = new();
}

public sealed class ForeshadowingReference
{
    public string ForeshadowingId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Importance { get; set; }
    public int ChaptersSinceSetup { get; set; }
    public bool IsReferenced { get; set; }
    public bool IsOverdue { get; set; }
    public double Confidence { get; set; }
}

public sealed class OverdueForeshadowing
{
    public string ForeshadowingId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Importance { get; set; }
    public int ChaptersSinceSetup { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
}
