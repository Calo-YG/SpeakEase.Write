using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateChapterOutlineTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_chapter_outline",
            Description = "创建或更新章节大纲。通过 id 或 volume_seq+chapter_title 查找已有章节，存在则更新标题和摘要，不存在则自动计算章节序号并创建，新卷时自动创建卷。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "章节ID（可选），用于更新已有章节" },
                    ["volume_seq"] = new() { Type = "integer", Description = "卷序号（新建必填，大于0）" },
                    ["volume_title"] = new() { Type = "string", Description = "卷标题（新卷时可用）" },
                    ["chapter_title"] = new() { Type = "string", Description = "章节标题（必填）" },
                    ["summary"] = new() { Type = "string", Description = "章节摘要/大纲内容（新建必填，更新可选）" }
                },
                Required = ["work_id", "chapter_title"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var id = args.GetString("id");
        var volumeSeq = args.GetInt32("volume_seq", defaultValue: 0, min: 1);
        var volumeTitle = args.GetString("volume_title");
        var chapterTitle = args.GetString("chapter_title", required: true);
        var summary = args.GetString("summary");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        ChapterEntity chapter = null;
        if (!string.IsNullOrEmpty(id))
            chapter = await db.Chapters.FirstOrDefaultAsync(c => c.Id == id && c.WorkId == workId, ct);
        if (chapter == null && volumeSeq > 0)
        {
            var volume = await db.Volumes.FirstOrDefaultAsync(v => v.WorkId == workId && v.Sequence == volumeSeq, ct);
            if (volume != null)
                chapter = await db.Chapters.FirstOrDefaultAsync(c => c.WorkId == workId && c.VolumeId == volume.Id && c.Title == chapterTitle, ct);
        }
        if (chapter == null)
            chapter = await db.Chapters.FirstOrDefaultAsync(c => c.WorkId == workId && c.Title == chapterTitle, ct);

        if (chapter != null)
        {
            chapter.Title = chapterTitle;
            if (!string.IsNullOrEmpty(summary)) chapter.Summary = summary;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"章节大纲已更新：第{chapter.Sequence}章「{chapterTitle}」，章节ID: {chapter.Id}");
        }

        var vol = await db.Volumes.FirstOrDefaultAsync(x => x.WorkId == workId && x.Sequence == volumeSeq, ct);
        if (vol == null)
        {
            vol = new VolumeEntity
            {
                Id = idGen.NextIdString(),
                WorkId = workId,
                Sequence = volumeSeq > 0 ? volumeSeq : 1,
                Title = volumeTitle ?? $"第{volumeSeq}卷",
                Summary = string.Empty
            };
            await db.Volumes.AddAsync(vol, ct);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var maxChapterSeq = await db.Chapters.AsNoTracking()
                .Where(x => x.WorkId == workId)
                .MaxAsync(x => (int?)x.Sequence, ct) ?? 0;

            var newChapter = new ChapterEntity
            {
                Id = idGen.NextIdString(),
                WorkId = workId,
                VolumeId = vol.Id,
                Sequence = maxChapterSeq + 1,
                Title = chapterTitle,
                Summary = summary ?? string.Empty,
                WordCount = 0,
                Status = "outline"
            };

            await db.Chapters.AddAsync(newChapter, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ToolResult.Ok($"章节大纲已创建，卷: 第{vol.Sequence}卷「{vol.Title}」，章节: 第{newChapter.Sequence}章「{chapterTitle}」，章节ID: {newChapter.Id}");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
