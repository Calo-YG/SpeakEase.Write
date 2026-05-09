using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetChapterBySequenceTool(IServiceScopeFactory scopeFactory,IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_chapter_by_sequence",
            Description = "根据作品标识和章节序号精确查询章节内容",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["sequence"] = new() { Type = "integer", Description = "章节序号" }
                },
                Required = ["work_id", "sequence"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null;
        int sequence = 0;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("sequence", out var s)) sequence = s.GetInt32();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (sequence <= 0) return ToolResult.Fail("缺少有效的 sequence 参数");

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
            return ToolResult.Fail(string.Format("未找到作品 {0} 的第 {1} 章", workId, sequence));

        return ToolResult.Ok(JsonSerializer.Serialize(chapter,snapshot.Value));
    }
}
