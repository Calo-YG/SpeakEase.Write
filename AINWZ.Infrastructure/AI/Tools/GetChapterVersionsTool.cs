using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetChapterVersionsTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_chapter_versions",
            Description = "查询章节的版本历史，包含手动保存、AI生成和自动保存的所有版本。用于回溯写作风格变化、找回被覆盖的内容、对比不同版本的差异。source 枚举: manual/autosave/ai-generate。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["chapter_id"] = new() { Type = "string", Description = "章节标识（必填）" },
                    ["limit"] = new() { Type = "integer", Description = "返回版本数量上限（默认5，范围1-20，按版本号倒序）" }
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

        var limit = args.Limit != 0 ? args.Limit : 5;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var chapter = await db.Chapters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == args.ChapterId && c.WorkId == args.WorkId, ct);

        if (chapter == null)
            return ToolResult.Fail($"未找到章节 {args.ChapterId}", "not_found");

        var versions = await db.ChapterVersions.AsNoTracking()
            .Where(v => v.ChapterId == args.ChapterId)
            .OrderByDescending(v => v.VersionNumber)
            .Take(limit)
            .ToListAsync(ct);

        if (versions.Count == 0)
            return ToolResult.Ok($"章节「{chapter.Title}」暂无版本历史");

        var sb = new StringBuilder();
        sb.AppendLine($"## 章节「{chapter.Title}」版本历史（最近 {versions.Count} 个版本）");
        sb.AppendLine();

        foreach (var v in versions)
        {
            sb.AppendLine($"### 版本 {v.VersionNumber}（{v.Source}）");
            if (!string.IsNullOrEmpty(v.Summary))
                sb.AppendLine($"  摘要: {v.Summary}");
            if (!string.IsNullOrEmpty(v.ModelId))
                sb.AppendLine($"  模型: {v.ModelId}");
            var contentPreview = v.Content?.Length > 200 ? v.Content[..200] + "..." : v.Content;
            sb.AppendLine($"  内容预览: {contentPreview}");
            sb.AppendLine();
        }

        return ToolResult.Ok(sb.ToString());
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string ChapterId { get; init; }
        public int Limit { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(ChapterId))
                return ToolResult.Fail("缺少必需参数 'chapter_id'", "argument_parse_error");
            if (Limit != 0 && (Limit < 1 || Limit > 20))
                return ToolResult.Fail($"参数 'limit' 值 {Limit} 超出范围 [1, 20]", "argument_parse_error");
            return null;
        }
    }
}
