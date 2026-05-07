using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateChapterOutlineTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_chapter_outline",
            Description = "创建一个章节骨架/占位（标题+摘要），会自动排序",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["volume_id"] = new() { Type = "string", Description = "所属卷标识（可选，无卷则留空）" },
                    ["title"] = new() { Type = "string", Description = "章节标题" },
                    ["summary"] = new() { Type = "string", Description = "章节内容摘要" },
                    ["sequence"] = new() { Type = "integer", Description = "章节序号（可选，默认自动递增）" }
                },
                Required = ["work_id", "title"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, volumeId = null, title = null, summary = null;
        int sequence = 0;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("volume_id", out var v)) volumeId = v.GetString();
            if (root.TryGetProperty("title", out var t)) title = t.GetString();
            if (root.TryGetProperty("summary", out var s)) summary = s.GetString();
            if (root.TryGetProperty("sequence", out var sq)) sequence = sq.GetInt32();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (string.IsNullOrEmpty(title)) return ToolResult.Fail("缺少 title 参数");

        if (sequence <= 0)
        {
            var maxSeq = await db.Chapters.AsNoTracking()
                .Where(x => x.WorkId == workId)
                .MaxAsync(x => (int?)x.Sequence, ct) ?? 0;
            sequence = maxSeq + 1;
        }

        var entity = new ChapterEntity
        {
            Id = Guid.NewGuid().ToString(),
            WorkId = workId,
            VolumeId = volumeId ?? string.Empty,
            Title = title,
            Summary = summary ?? string.Empty,
            Sequence = sequence,
            Status = "outline",
            Content = string.Empty
        };

        db.Chapters.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok(string.Format("章节骨架「{0}」已创建，sequence: {1}, id: {2}", title, sequence, entity.Id));
    }
}
