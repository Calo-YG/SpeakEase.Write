using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 章节正文保存工具：保存/更新章节正文内容，章节不存在时自动创建（同时自动创建缺少的卷），写完后自动重算全文字数
public sealed class SaveChapterContentTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "save_chapter_content",
            Description =
                "保存章节正文内容到数据库，若章节不存在则自动创建。优先按 chapter_id 精确匹配，其次按 chapter_sequence 查找。若章节不存在且提供了 chapter_sequence，将自动创建新章节（同时自动创建缺少的卷）。章节写作完成后必须调用，否则正文丢失。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["content"] = new() { Type = "string", Description = "章节完整正文内容（必填）" },
                    ["chapter_id"] = new() { Type = "string", Description = "章节ID，优先使用。从 get_recent_chapters/ get_chapter/ create_chapter_outline 等工具返回结果中获取" },
                    ["chapter_sequence"] = new() { Type = "integer", Description = "章节序号。如用户说「写第3章」则传 3。chapter_id 无法确定或章节不存在时使用" },
                    ["chapter_title"] = new() { Type = "string", Description = "章节标题。新建章节时必填，更新已有章节时可选" }
                },
                Required = ["work_id", "content"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        ChapterEntity chapter = null;

        if (!string.IsNullOrEmpty(args.ChapterId))
            chapter = await db.Chapters.FirstOrDefaultAsync(
                c => c.Id == args.ChapterId && c.WorkId == args.WorkId, ct);

        if (chapter == null && args.ChapterSequence > 0)
            chapter = await db.Chapters.FirstOrDefaultAsync(
                c => c.WorkId == args.WorkId && c.Sequence == args.ChapterSequence, ct);

        if (chapter != null)
            return await UpdateExisting(db, chapter, args.Content, ct);

        return await CreateNew(db, idGen, args.WorkId, args.ChapterSequence, args.ChapterTitle, args.Content, ct);
    }

    private static async Task<ToolResult> UpdateExisting(
        SpeakEaseDbContext db, ChapterEntity chapter, string content, CancellationToken ct)
    {
        chapter.Content = content;
        chapter.WordCount = content.Count(c => !char.IsWhiteSpace(c));
        chapter.LastContentSavedAt = DateTime.Now;
        if (chapter.Status == "outline")
            chapter.Status = "completed";
        await db.SaveChangesAsync(ct);

        await RecalcTotalWords(db, chapter.WorkId, ct);

        var result = ToolResult.Ok(
            $"第{chapter.Sequence}章「{chapter.Title}」正文已更新，共 {chapter.WordCount} 字。");
        result.ContentType = "chapter";
        result.ExtraData = new Dictionary<string, string>
        {
            ["chapterId"] = chapter.Id,
            ["sequence"] = chapter.Sequence.ToString(),
            ["title"] = chapter.Title,
            ["content"] = content
        };
        return result;
    }

    private static async Task<ToolResult> CreateNew(
        SpeakEaseDbContext db, ISnowflakeIdGenerator idGen,
        string workId, int chapterSequence, string chapterTitle, string content, CancellationToken ct)
    {
        var volumes = await db.Volumes
            .Where(v => v.WorkId == workId)
            .OrderBy(v => v.Sequence)
            .Select(v => new { v.Id, v.Sequence, v.Title })
            .ToListAsync(ct);

        VolumeEntity volume;
        if (volumes.Count > 0)
        {
            volume = new VolumeEntity { Id = volumes[^1].Id, Sequence = volumes[^1].Sequence, Title = volumes[^1].Title };
        }
        else
        {
            volume = new VolumeEntity
            {
                Id = idGen.NextIdString(),
                WorkId = workId,
                Sequence = 1,
                Title = "第1卷",
                Summary = string.Empty
            };
            await db.Volumes.AddAsync(volume, ct);
            await db.SaveChangesAsync(ct);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var maxSeq = await db.Chapters.AsNoTracking()
                .Where(c => c.WorkId == workId)
                .MaxAsync(c => (int?)c.Sequence, ct) ?? 0;

            var seq = chapterSequence > 0 ? chapterSequence : maxSeq + 1;
            var title = chapterTitle ?? $"第{seq}章";

            var chapter = new ChapterEntity
            {
                Id = idGen.NextIdString(),
                WorkId = workId,
                VolumeId = volume.Id,
                Sequence = seq,
                Title = title,
                Content = content,
                WordCount = content.Count(c => !char.IsWhiteSpace(c)),
                Summary = string.Empty,
                LastContentSavedAt = DateTime.Now,
                Status = "completed"
            };
            await db.Chapters.AddAsync(chapter, ct);
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            await RecalcTotalWords(db, workId, ct);

            var result = ToolResult.Ok(
                $"新章节已创建并保存：第{seq}章「{title}」，共 {chapter.WordCount} 字。章节ID: {chapter.Id}");
            result.ContentType = "chapter";
            result.ExtraData = new Dictionary<string, string>
            {
                ["chapterId"] = chapter.Id,
                ["sequence"] = seq.ToString(),
                ["title"] = title,
                ["content"] = content
            };
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task RecalcTotalWords(SpeakEaseDbContext db, string workId, CancellationToken ct)
    {
        var totalWords = await db.Chapters.AsNoTracking()
            .Where(c => c.WorkId == workId)
            .SumAsync(c => c.WordCount, ct);

        await db.Works
            .Where(w => w.Id == workId)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.TotalWordCount, totalWords)
                                      .SetProperty(w => w.UpdateAt, DateTime.Now), ct);
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Content { get; init; }
        public string ChapterId { get; init; }
        public int ChapterSequence { get; init; }
        public string ChapterTitle { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Content))
                return ToolResult.Fail("缺少必需参数 'content'", "argument_parse_error");
            return null;
        }
    }
}
