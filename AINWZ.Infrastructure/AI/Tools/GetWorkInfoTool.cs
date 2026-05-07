using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetWorkInfoTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_work_info",
            Description = "获取作品的完整基本信息（简介、题材、风格、视角、字数等）",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("work_id", out var w)) workId = w.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");

        var work = await db.Works.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == workId, ct);

        if (work == null)
            return ToolResult.Fail(string.Format("未找到作品 {0}", workId));

        var chapterCount = await db.Chapters.AsNoTracking()
            .CountAsync(x => x.WorkId == workId, ct);

        var volumeCount = await db.Volumes.AsNoTracking()
            .CountAsync(x => x.WorkId == workId, ct);

        var characterCount = await db.Characters.AsNoTracking()
            .CountAsync(x => x.WorkId == workId, ct);

        return ToolResult.Ok(JsonSerializer.Serialize(new
        {
            work.Title,
            work.Summary,
            work.Genre,
            work.Perspective,
            work.StyleTags,
            work.CreationMode,
            work.Status,
            work.TotalWordCount,
            chapterCount,
            volumeCount,
            characterCount
        }));
    }
}
