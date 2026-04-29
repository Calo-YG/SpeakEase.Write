using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

public sealed class WritingBlackboardBuilder
{
    private readonly SpeakEaseDbContext _db;

    public WritingBlackboardBuilder(SpeakEaseDbContext db) => _db = db;

    public async Task<WritingBlackboard> BuildAsync(string workId, string requestId)
    {
        var board = new WritingBlackboard
        {
            WorkId = workId,
            RequestId = requestId
        };

        var work = await _db.Works.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == workId);

        if (work != null)
        {
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
                .Take(5)
                .ToListAsync();

            board.RecentChapters = chapters
                .OrderBy(c => c.Sequence)
                .Select(c => new ChapterSection
                {
                    ChapterId = c.Id,
                    Sequence = c.Sequence,
                    Title = c.Title ?? string.Empty,
                    Content = c.Content ?? string.Empty,
                    Summary = c.Summary ?? string.Empty
                })
                .ToList();

            var characters = await _db.Characters.AsNoTracking()
                .Where(x => x.WorkId == workId)
                .ToListAsync();

            board.Characters = characters
                .Select(c => new CharacterSection
                {
                    CharacterId = c.Id,
                    Name = c.Name ?? string.Empty,
                    CoreSeed = c.Identity ?? string.Empty
                })
                .ToList();
        }

        return board;
    }
}
