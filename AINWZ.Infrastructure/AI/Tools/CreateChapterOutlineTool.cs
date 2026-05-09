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
            Description = "创建章节大纲。将自动计算章节序号，并在新卷时自动创建卷。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["volume_seq"] = new() { Type = "integer", Description = "卷序号（必填，大于0）" },
                    ["volume_title"] = new() { Type = "string", Description = "卷标题（新卷时必填）" },
                    ["chapter_title"] = new() { Type = "string", Description = "章节标题（必填）" },
                    ["summary"] = new() { Type = "string", Description = "章节摘要/大纲内容（必填）" }
                },
                Required = ["work_id", "volume_seq", "chapter_title", "summary"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var volumeSeq = args.GetInt32("volume_seq", required: true, min: 1);
        var volumeTitle = args.GetString("volume_title");
        var chapterTitle = args.GetString("chapter_title", required: true);
        var summary = args.GetString("summary", required: true);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var volume = await db.Volumes.FirstOrDefaultAsync(
            x => x.WorkId == workId && x.Sequence == volumeSeq, ct);

        if (volume == null)
        {
            volume = new VolumeEntity
            {
                Id = idGen.NextIdString(),
                WorkId = workId,
                Sequence = volumeSeq,
                Title = volumeTitle ?? $"第{volumeSeq}卷",
                Summary = string.Empty
            };
            await db.Volumes.AddAsync(volume, ct);
            await db.SaveChangesAsync(ct);
        }

        var maxChapterSeq = await db.Chapters.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .MaxAsync(x => (int?)x.Sequence, ct) ?? 0;

        var chapter = new ChapterEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            VolumeId = volume.Id,
            Sequence = maxChapterSeq + 1,
            Title = chapterTitle,
            Summary = summary,
            WordCount = 0,
            Status = "outline"
        };

        await db.Chapters.AddAsync(chapter, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok(
            $"章节大纲已创建，卷: 第{volumeSeq}卷「{volume.Title}」，章节: 第{chapter.Sequence}章「{chapterTitle}」，章节ID: {chapter.Id}");
    }
}
