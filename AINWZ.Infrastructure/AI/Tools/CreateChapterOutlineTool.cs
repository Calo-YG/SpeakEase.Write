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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        ChapterEntity chapter = null;
        if (!string.IsNullOrEmpty(args.Id))
            chapter = await db.Chapters.FirstOrDefaultAsync(c => c.Id == args.Id && c.WorkId == args.WorkId, ct);
        if (chapter == null && args.VolumeSeq > 0)
        {
            var volume = await db.Volumes.FirstOrDefaultAsync(v => v.WorkId == args.WorkId && v.Sequence == args.VolumeSeq, ct);
            if (volume != null)
                chapter = await db.Chapters.FirstOrDefaultAsync(c => c.WorkId == args.WorkId && c.VolumeId == volume.Id && c.Title == args.ChapterTitle, ct);
        }
        if (chapter == null)
            chapter = await db.Chapters.FirstOrDefaultAsync(c => c.WorkId == args.WorkId && c.Title == args.ChapterTitle, ct);

        if (chapter != null)
        {
            chapter.Title = args.ChapterTitle;
            if (!string.IsNullOrEmpty(args.Summary)) chapter.Summary = args.Summary;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"章节大纲已更新：第{chapter.Sequence}章「{args.ChapterTitle}」，章节ID: {chapter.Id}");
        }

        var vol = await db.Volumes.FirstOrDefaultAsync(x => x.WorkId == args.WorkId && x.Sequence == args.VolumeSeq, ct);
        if (vol == null)
        {
            vol = new VolumeEntity
            {
                Id = idGen.NextIdString(),
                WorkId = args.WorkId,
                Sequence = args.VolumeSeq > 0 ? args.VolumeSeq : 1,
                Title = args.VolumeTitle ?? $"第{args.VolumeSeq}卷",
                Summary = string.Empty
            };
            await db.Volumes.AddAsync(vol, ct);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var maxChapterSeq = await db.Chapters.AsNoTracking()
                .Where(x => x.WorkId == args.WorkId)
                .MaxAsync(x => (int?)x.Sequence, ct) ?? 0;

            var newChapter = new ChapterEntity
            {
                Id = idGen.NextIdString(),
                WorkId = args.WorkId,
                VolumeId = vol.Id,
                Sequence = maxChapterSeq + 1,
                Title = args.ChapterTitle,
                Summary = args.Summary ?? string.Empty,
                WordCount = 0,
                Status = "outline"
            };

            await db.Chapters.AddAsync(newChapter, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ToolResult.Ok($"章节大纲已创建，卷: 第{vol.Sequence}卷「{vol.Title}」，章节: 第{newChapter.Sequence}章「{args.ChapterTitle}」，章节ID: {newChapter.Id}");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public int VolumeSeq { get; init; }
        public string VolumeTitle { get; init; }
        public string ChapterTitle { get; init; }
        public string Summary { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(ChapterTitle))
                return ToolResult.Fail("缺少必需参数 'chapter_title'", "argument_parse_error");
            if (VolumeSeq != 0 && VolumeSeq < 1)
                return ToolResult.Fail($"参数 'volume_seq' 值 {VolumeSeq} 超出范围 [1, ∞]", "argument_parse_error");
            return null;
        }
    }
}
