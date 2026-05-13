using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

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
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var sequence = args.GetInt32("sequence", required: true, min: 1);
        var maxContentChars = args.GetInt32("max_content_chars", defaultValue: 4000, min: 500, max: 20000);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var chapter = await db.Chapters.AsNoTracking()
            .Where(x => x.WorkId == workId && x.Sequence == sequence)
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
            return ToolResult.Fail($"未找到作品 {workId} 的第 {sequence} 章", "not_found");

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
}
