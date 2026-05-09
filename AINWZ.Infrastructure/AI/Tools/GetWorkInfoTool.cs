using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetWorkInfoTool(IServiceScopeFactory scopeFactory, IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
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
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var work = await db.Works.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == workId, ct);

        if (work == null)
            return ToolResult.Fail($"未找到作品 {workId}", "not_found");

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
        }, snapshot.Value));
    }
}
