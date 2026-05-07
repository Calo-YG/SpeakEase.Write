using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateForeshadowingTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_foreshadowing",
            Description = "创建一条伏笔，支持指定重要性(1-10)和关联章节",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["title"] = new() { Type = "string", Description = "伏笔标题（简练，让读者产生好奇）" },
                    ["description"] = new() { Type = "string", Description = "伏笔详细描述" },
                    ["importance"] = new() { Type = "integer", Description = "重要性 1-10（默认 5）" },
                    ["setup_chapter_id"] = new() { Type = "string", Description = "埋设章节标识" },
                    ["payoff_chapter_id"] = new() { Type = "string", Description = "预期回收章节标识（可选）" }
                },
                Required = ["work_id", "title", "description"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, title = null, description = null, setupChId = null, payoffChId = null;
        int importance = 5;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("title", out var t)) title = t.GetString();
            if (root.TryGetProperty("description", out var d)) description = d.GetString();
            if (root.TryGetProperty("importance", out var im)) importance = im.GetInt32();
            if (root.TryGetProperty("setup_chapter_id", out var sc)) setupChId = sc.GetString();
            if (root.TryGetProperty("payoff_chapter_id", out var pc)) payoffChId = pc.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (string.IsNullOrEmpty(title)) return ToolResult.Fail("缺少 title 参数");
        if (string.IsNullOrEmpty(description)) return ToolResult.Fail("缺少 description 参数");
        if (importance < 1) importance = 1;
        if (importance > 10) importance = 10;

        var entity = new ForeshadowingEntity
        {
            Id = Guid.NewGuid().ToString(),
            WorkId = workId,
            Title = title,
            Description = description,
            Importance = importance,
            SetupChapterId = setupChId ?? string.Empty,
            PayoffChapterId = payoffChId ?? string.Empty,
            Status = "pending"
        };

        db.Foreshadowings.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok(string.Format("伏笔「{0}」已创建，id: {1}", title, entity.Id));
    }
}
