using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetChapterTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GetChapterTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Function = new FunctionDefinition
        {
            Name = "get_chapter",
            Description = "按章节ID查询单个章节的完整信息（标题、正文、摘要、字数）。",
            Parameters = new FunctionParameters
            {
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID" },
                    ["chapter_id"] = new() { Type = "string", Description = "章节ID" }
                },
                Required = new List<string> { "work_id", "chapter_id" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        string workId = null;
        string chapterId = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (doc.RootElement.TryGetProperty("chapter_id", out var prop))
                chapterId = prop.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId) || string.IsNullOrEmpty(chapterId))
            return new ToolResult { Success = false, Content = "缺少必要参数", ErrorCode = "missing_parameter" };

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
            return new ToolResult { Success = false, Content = $"未找到章节 {chapterId}", ErrorCode = "not_found" };

        return new ToolResult
        {
            Success = true,
            Content = $"## 第{chapter.Sequence}章：{chapter.Title ?? "未命名"}\n" +
                      $"状态：{chapter.Status ?? "draft"} | 字数：{chapter.WordCount}\n" +
                      (!string.IsNullOrEmpty(chapter.Summary) ? $"摘要：{chapter.Summary}\n" : "") +
                      chapter.Content
        };
    }
}
