using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetRecentChaptersTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GetRecentChaptersTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Function = new FunctionDefinition
        {
            Name = "get_recent_chapters",
            Description = "查询最近N个章节的完整内容（含标题、正文、摘要、字数），用于续写时参考上下文。",
            Parameters = new FunctionParameters
            {
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID" },
                    ["count"] = new() { Type = "integer", Description = "查询章节数量（默认3）" }
                },
                Required = new List<string> { "work_id" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        string workId = null;
        int count = 3;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (doc.RootElement.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number)
                count = c.GetInt32();
        }
        catch { }

        if (string.IsNullOrEmpty(workId))
            return new ToolResult { Success = false, Content = "缺少 work_id 参数", ErrorCode = "missing_parameter" };

        if (count < 1) count = 1;
        if (count > 10) count = 10;

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
            return new ToolResult { Content = "暂无章节" };

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

        return new ToolResult
        {
            Success = true,
            Content = sb.ToString()
        };
    }
}
