using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 章节查询工具：按章节ID查询单个章节的完整信息（标题/正文/摘要/字数），支持内容截断
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        var maxContentChars = args.MaxContentChars != 0 ? args.MaxContentChars : 4000;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IStoryDbContext>();

        var chapter = await db.Chapters.AsNoTracking()
            .Where(c => c.Id == args.ChapterId && c.WorkId == args.WorkId)
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
            return ToolResult.Fail($"未找到章节 {args.ChapterId}", "not_found");

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

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string ChapterId { get; init; }
        public int MaxContentChars { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(ChapterId))
                return ToolResult.Fail("缺少必需参数 'chapter_id'", "argument_parse_error");
            if (MaxContentChars != 0 && (MaxContentChars < 500 || MaxContentChars > 20000))
                return ToolResult.Fail($"参数 'max_content_chars' 值 {MaxContentChars} 超出范围 [500, 20000]", "argument_parse_error");
            return null;
        }
    }
}
