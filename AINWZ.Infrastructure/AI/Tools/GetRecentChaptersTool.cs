using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 最近章节查询工具：返回作品尾部最近N章或指定章节附近的前后均衡窗口，用于续写参考上下文
public sealed class GetRecentChaptersTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_recent_chapters",
            Description =
                "查询章节完整内容用于续写参考。不传 chapter_sequence 时取作品尾部最近 N 章；传 chapter_sequence 时以该章节为中心向前后均衡扩展，自动处理首章/末章边界。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["count"] = new() { Type = "integer", Description = "返回的章节数量（默认3，范围1-10）" },
                    ["chapter_sequence"] = new() { Type = "integer", Description = "锚定章节序号。续写第 N 章时传 N，工具将返回以第 N 章附近的前后均衡窗口。不传则取作品尾部最近章节" }
                },
                Required = ["work_id"]
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

        var count = args.Count != 0 ? args.Count : 3;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var baseQuery = db.Chapters.AsNoTracking()
            .Where(c => c.WorkId == args.WorkId);

        if (args.ChapterSequence > 0)
        {
            var maxSeq = await baseQuery.MaxAsync(c => (int?)c.Sequence, ct) ?? 0;

            var halfBefore = (count - 1) / 2;
            var remainingAfter = count - 1 - halfBefore;

            var start = args.ChapterSequence - halfBefore;
            if (start < 1)
            {
                remainingAfter += 1 - start;
                start = 1;
            }

            var end = args.ChapterSequence + remainingAfter;
            if (end > maxSeq)
            {
                start = Math.Max(1, start - (end - maxSeq));
                end = maxSeq;
            }

            var chapters = await baseQuery
                .Where(c => c.Sequence >= start && c.Sequence <= end)
                .OrderBy(c => c.Sequence)
                .Take(count)
                .Select(c => new ChapterRow
                {
                    Id = c.Id, Sequence = c.Sequence, Title = c.Title ?? string.Empty,
                    Summary = c.Summary ?? string.Empty, WordCount = c.WordCount, Status = c.Status ?? string.Empty,
                    Content = c.Content ?? string.Empty
                })
                .ToListAsync(ct);

            return FormatResult(chapters, $"以第{args.ChapterSequence}章为中心（窗口 [{start}, {end}]）");
        }
        else
        {
            var chapters = await baseQuery
                .OrderByDescending(c => c.Sequence)
                .Take(count)
                .OrderBy(c => c.Sequence)
                .Select(c => new ChapterRow
                {
                    Id = c.Id, Sequence = c.Sequence, Title = c.Title ?? string.Empty,
                    Summary = c.Summary ?? string.Empty, WordCount = c.WordCount, Status = c.Status ?? string.Empty,
                    Content = c.Content ?? string.Empty
                })
                .ToListAsync(ct);

            return FormatResult(chapters, "尾部最近");
        }
    }

    private static ToolResult FormatResult(List<ChapterRow> chapters, string windowDesc)
    {
        if (chapters.Count == 0)
            return ToolResult.Ok("暂无章节");

        var sb = new StringBuilder();
        sb.AppendLine($"[窗口] {windowDesc}，共 {chapters.Count} 章");
        sb.AppendLine();

        foreach (var ch in chapters)
        {
            sb.AppendLine($"## 第{ch.Sequence}章 (ID: {ch.Id})：{ch.Title}");
            sb.AppendLine($"状态：{ch.Status} | 字数：{ch.WordCount}");
            if (!string.IsNullOrEmpty(ch.Summary))
                sb.AppendLine($"摘要：{ch.Summary}");
            sb.AppendLine(ch.Content);
            sb.AppendLine();
        }

        return ToolResult.Ok(sb.ToString());
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public int Count { get; init; }
        public int ChapterSequence { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (Count != 0 && (Count < 1 || Count > 10))
                return ToolResult.Fail($"参数 'count' 值 {Count} 超出范围 [1, 10]", "argument_parse_error");
            return null;
        }
    }

    private sealed class ChapterRow
    {
        public string Id { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
