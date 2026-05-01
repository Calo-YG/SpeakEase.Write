using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetTimelineEventsTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GetTimelineEventsTool(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_timeline_events",
            Description = "查询作品的时间线事件列表，可按事件类型过滤，按时间正序返回。用于了解故事时间脉络、确认前文事件、避免时间线矛盾。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["event_type"] = new() { Type = "string", Description = "按事件类型过滤: plot/character/world/backstory（不传返回全部）" },
                    ["limit"] = new() { Type = "integer", Description = "返回数量上限（默认 20，最大 50）" }
                },
                Required = new List<string> { "work_id" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, eventType = null;
        int limit = 20;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("event_type", out var et)) eventType = et.GetString();
            if (root.TryGetProperty("limit", out var l)) limit = l.GetInt32();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (limit < 1) limit = 1;
        if (limit > 50) limit = 50;

        var query = db.TimelineEvents.AsNoTracking()
            .Where(x => x.WorkId == workId);

        if (!string.IsNullOrEmpty(eventType))
            query = query.Where(x => x.EventType == eventType);

        var events = await query
            .OrderBy(x => x.EventTime)
            .Take(limit)
            .Select(x => new
            {
                id = x.Id,
                title = x.Title,
                description = x.Description,
                event_time = x.EventTime,
                event_type = x.EventType,
                chapter_id = x.ChapterId,
                related_character_ids = x.RelatedCharacterIds
            })
            .ToListAsync(ct);

        if (events.Count == 0)
            return ToolResult.Ok("暂无时间线事件记录。");

        return ToolResult.Ok(JsonSerializer.Serialize<object>(events));
    }
}
