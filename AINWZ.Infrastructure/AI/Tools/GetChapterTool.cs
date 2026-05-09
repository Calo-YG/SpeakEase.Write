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
                    ["chapter_id"] = new() { Type = "string", Description = "章节ID（必填）" }
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
                Content = c.Content ?? string.Empty
            })
            .FirstOrDefaultAsync(ct);

        if (chapter == null)
            return ToolResult.Fail($"未找到章节 {chapterId}", "not_found");

        return ToolResult.Ok(
            $"## 第{chapter.Sequence}章：{chapter.Title ?? "未命名"}\n" +
            $"状态：{chapter.Status ?? "draft"} | 字数：{chapter.WordCount}\n" +
            (!string.IsNullOrEmpty(chapter.Summary) ? $"摘要：{chapter.Summary}\n" : "") +
            chapter.Content);
    }
}
