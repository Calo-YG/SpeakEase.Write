using System.Text.Json;
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
            Description = "为指定章节生成或更新摘要，摘要应简洁概括本章核心情节，100-200字",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["chapter_id"] = new() { Type = "string", Description = "章节标识" },
                    ["summary"] = new() { Type = "string", Description = "章节摘要内容，100-200字" }
                },
                Required = ["work_id", "chapter_id", "summary"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, chapterId = null, summary = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("chapter_id", out var c)) chapterId = c.GetString();
            if (root.TryGetProperty("summary", out var s)) summary = s.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (string.IsNullOrEmpty(chapterId)) return ToolResult.Fail("缺少 chapter_id 参数");
        if (string.IsNullOrEmpty(summary)) return ToolResult.Fail("缺少 summary 参数");

        var entity = await db.Chapters
            .FirstOrDefaultAsync(x => x.Id == chapterId && x.WorkId == workId, ct);

        if (entity is null)
            return ToolResult.Fail($"章节(id={chapterId})不存在");

        entity.Summary = summary;
        entity.UpdateAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"章节「{entity.Title}」摘要已更新");
    }
}
