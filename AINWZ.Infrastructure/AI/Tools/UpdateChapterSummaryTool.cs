using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 章节摘要更新工具：更新章节的摘要信息，章节正文写完后生成精炼摘要供后续参考
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

        var chapter = await db.Chapters.FirstOrDefaultAsync(
            c => c.Id == args.ChapterId && c.WorkId == args.WorkId, ct);

        if (chapter == null)
            return ToolResult.Fail($"未找到章节 {args.ChapterId}", "not_found");

        chapter.Summary = args.Summary;
        chapter.UpdateAt = DateTime.Now;
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"章节「{chapter.Title}」摘要已更新");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string ChapterId { get; init; }
        public string Summary { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(ChapterId))
                return ToolResult.Fail("缺少必需参数 'chapter_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Summary))
                return ToolResult.Fail("缺少必需参数 'summary'", "argument_parse_error");
            return null;
        }
    }
}
