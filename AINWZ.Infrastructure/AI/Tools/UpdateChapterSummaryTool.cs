using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class UpdateChapterSummaryTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "update_chapter_summary",
            Description = "更新章节的摘要信息。章节正文写完后，应调用此工具生成精炼的章节摘要，供后续章节写作参考。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["chapter_id"] = new() { Type = "string", Description = "章节ID（必填）" },
                    ["summary"] = new() { Type = "string", Description = "新的章节摘要内容（必填），包含关键情节、人物行为和重要对话" }
                },
                Required = ["work_id", "chapter_id", "summary"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var chapterId = args.GetString("chapter_id", required: true);
        var summary = args.GetString("summary", required: true);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var chapter = await db.Chapters.FirstOrDefaultAsync(
            c => c.Id == chapterId && c.WorkId == workId, ct);

        if (chapter == null)
            return ToolResult.Fail($"未找到章节 {chapterId}", "not_found");

        chapter.Summary = summary;
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"章节「{chapter.Title}」摘要已更新");
    }
}
