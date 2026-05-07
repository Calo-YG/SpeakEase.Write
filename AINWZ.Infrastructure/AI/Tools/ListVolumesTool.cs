using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class ListVolumesTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "list_volumes",
            Description = "列出作品的所有卷，包含卷序号、标题和所含章节概览",
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

        var volumes = await db.Volumes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(ct);

        var chapters = await db.Chapters.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(ct);

        var result = volumes.Select(v => new
        {
            v.Sequence,
            v.Title,
            v.Summary,
            chapters = chapters.Where(c => c.VolumeId == v.Id)
                .OrderBy(c => c.Sequence)
                .Select(c => new { c.Sequence, c.Title, c.Summary, c.WordCount, c.Status })
        });

        return ToolResult.Ok(JsonSerializer.Serialize(result));
    }
}
