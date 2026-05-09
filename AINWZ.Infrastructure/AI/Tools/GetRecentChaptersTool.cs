using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetRecentChaptersTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_recent_chapters",
            Description = "查询最近N个章节的完整内容（含标题、正文、摘要、字数），用于续写时参考上下文。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["count"] = new() { Type = "integer", Description = "查询章节数量（默认3，范围1-10）" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var count = args.GetInt32("count", defaultValue: 3, min: 1, max: 10);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var chapters = await db.Chapters.AsNoTracking()
            .Where(c => c.WorkId == workId)
            .OrderByDescending(c => c.Sequence)
            .Take(count)
            .OrderBy(c => c.Sequence)
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
            .ToListAsync(ct);

        if (chapters.Count == 0)
            return ToolResult.Ok("暂无章节");

        var sb = new StringBuilder();
        foreach (var ch in chapters)
        {
            sb.AppendLine($"## 第{ch.Sequence}章：{ch.Title ?? "未命名"}");
            sb.AppendLine($"状态：{ch.Status ?? "draft"} | 字数：{ch.WordCount}");
            if (!string.IsNullOrEmpty(ch.Summary))
                sb.AppendLine($"摘要：{ch.Summary}");
            sb.AppendLine(ch.Content);
            sb.AppendLine();
        }

        return ToolResult.Ok(sb.ToString());
    }
}
