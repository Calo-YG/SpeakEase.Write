using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 按序号查询章节工具：根据作品ID和章节序号精确查询章节内容，超长时自动截断
public sealed class GetChapterBySequenceTool(IServiceScopeFactory scopeFactory, IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_chapter_by_sequence",
            Description = "根据作品标识和章节序号精确查询章节内容，正文超过4000字符时自动截断。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["sequence"] = new() { Type = "integer", Description = "章节序号（必填，大于0的整数）" },
                    ["max_content_chars"] = new() { Type = "integer", Description = "正文最大返回字符数（默认4000，超长截断标注）" }
                },
                Required = ["work_id", "sequence"]
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
            .Where(x => x.WorkId == args.WorkId && x.Sequence == args.Sequence)
            .Select(x => new
            {
                x.Id,
                x.Sequence,
                x.Title,
                x.Summary,
                x.Content,
                x.WordCount,
                x.Status
            })
            .FirstOrDefaultAsync(ct);

        if (chapter == null)
            return ToolResult.Fail($"未找到作品 {args.WorkId} 的第 {args.Sequence} 章", "not_found");

        var content = chapter.Content;
        if (content != null && content.Length > maxContentChars)
            content = content[..maxContentChars] + $"\n\n…（内容已截断，共 {chapter.WordCount} 字，截取前 {maxContentChars} 字符）";

        return ToolResult.Ok(JsonSerializer.Serialize(new
        {
            chapter.Id,
            chapter.Sequence,
            chapter.Title,
            chapter.Summary,
            Content = content,
            chapter.WordCount,
            chapter.Status
        }, snapshot.Value));
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public int Sequence { get; init; }
        public int MaxContentChars { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (Sequence == 0)
                return ToolResult.Fail("缺少必需参数 'sequence'", "argument_parse_error");
            if (Sequence < 1)
                return ToolResult.Fail($"参数 'sequence' 值 {Sequence} 超出范围 [1, ∞]", "argument_parse_error");
            if (MaxContentChars != 0 && (MaxContentChars < 500 || MaxContentChars > 20000))
                return ToolResult.Fail($"参数 'max_content_chars' 值 {MaxContentChars} 超出范围 [500, 20000]", "argument_parse_error");
            return null;
        }
    }
}
