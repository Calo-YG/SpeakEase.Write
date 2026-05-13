using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetChapterTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_chapter",
            Description = "按章节ID查询单个章节的完整信息（标题、正文、摘要、字数）。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["chapter_id"] = new() { Type = "string", Description = "章节ID（必填）" },
                    ["max_content_chars"] = new() { Type = "integer", Description = "正文最大返回字符数（默认4000，超长截断标注）" }
                },
                Required = ["work_id", "chapter_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var chapterId = args.GetString("chapter_id", required: true);
        var maxContentChars = args.GetInt32("max_content_chars", defaultValue: 4000, min: 500, max: 20000);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var chapter = await db.Chapters.AsNoTracking()
            .Where(c => c.Id == chapterId && c.WorkId == workId)
            .Select(c => new
            {
                c.Id,
                c.Sequence,
                c.Title,
                c.Summary,
                c.WordCount,
                c.Status,
                c.VolumeId,
                c.OutlineNodeIds,
                c.AuthorNotes,
                Content = c.Content ?? string.Empty
            })
            .FirstOrDefaultAsync(ct);

        if (chapter == null)
            return ToolResult.Fail($"未找到章节 {chapterId}", "not_found");

        var content = chapter.Content;
        if (content.Length > maxContentChars)
            content = content[..maxContentChars] + $"\n\n…（内容已截断，共 {chapter.WordCount} 字，截取前 {maxContentChars} 字符）";

        var result = $"## 第{chapter.Sequence}章：{chapter.Title ?? "未命名"}\n" +
            $"状态：{chapter.Status ?? "draft"} | 字数：{chapter.WordCount}\n" +
            $"卷ID：{chapter.VolumeId}\n" +
            (!string.IsNullOrEmpty(chapter.Summary) ? $"摘要：{chapter.Summary}\n" : "") +
            (chapter.OutlineNodeIds is { Count: > 0 }
                ? $"关联大纲节点：{string.Join(", ", chapter.OutlineNodeIds)}\n" : "") +
            (!string.IsNullOrEmpty(chapter.AuthorNotes) ? $"作者备注：{chapter.AuthorNotes}\n" : "") +
            content;

        return ToolResult.Ok(result);
    }
}
