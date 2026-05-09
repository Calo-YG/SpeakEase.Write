using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateTimelineEventTool(IServiceScopeFactory scopeFactory,IOptionsSnapshot<JsonSerializerOptions> options) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_timeline_event",
            Description = "创建一个时间线事件，用于追踪故事中的关键时间节点。事件类型包括：plot（情节推进）、character（角色转折）、world（世界变动）、backstory（前史揭秘）。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["title"] = new() { Type = "string", Description = "事件标题（简洁概括）" },
                    ["description"] = new() { Type = "string", Description = "事件详细描述" },
                    ["event_time"] = new() { Type = "string", Description = "事件发生时间（故事内时间，ISO格式或自由文本）" },
                    ["event_type"] = new() { Type = "string", Description = "事件类型: plot/character/world/backstory" },
                    ["chapter_id"] = new() { Type = "string", Description = "关联章节标识（可选）" },
                    ["related_character_ids"] = new()
                    {
                        Type = "array",
                        Description = "关联角色标识数组（可选）",
                        Items = new ParameterSchema { Type = "string" }
                    }
                },
                Required = ["work_id", "title", "description", "event_type"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        string workId = null, title = null, description = null, eventTimeStr = null, eventType = null, chapterId = null;
        List<string> relatedCharacterIds = new();
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("title", out var t)) title = t.GetString();
            if (root.TryGetProperty("description", out var d)) description = d.GetString();
            if (root.TryGetProperty("event_time", out var et)) eventTimeStr = et.GetString();
            if (root.TryGetProperty("event_type", out var etype)) eventType = etype.GetString();
            if (root.TryGetProperty("chapter_id", out var ch)) chapterId = ch.GetString();
            if (root.TryGetProperty("related_character_ids", out var rc) && rc.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in rc.EnumerateArray())
                {
                    var id = item.GetString();
                    if (!string.IsNullOrEmpty(id))
                        relatedCharacterIds.Add(id);
                }
            }
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (string.IsNullOrEmpty(title)) return ToolResult.Fail("缺少 title 参数");
        if (string.IsNullOrEmpty(description)) return ToolResult.Fail("缺少 description 参数");

        var allowedTypes = new HashSet<string> { "plot", "character", "world", "backstory" };
        eventType = (eventType ?? "plot").ToLowerInvariant();
        if (!allowedTypes.Contains(eventType))
            eventType = "plot";

        DateTime eventTime = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(eventTimeStr))
        {
            if (DateTime.TryParse(eventTimeStr, out var parsed))
                eventTime = parsed;
        }

        if (!string.IsNullOrEmpty(chapterId))
        {
            var chapterExists = await db.Chapters.AnyAsync(c => c.Id == chapterId && c.WorkId == workId, ct);
            if (!chapterExists)
                chapterId = string.Empty;
        }

        var entity = new TimelineEventEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            ChapterId = chapterId ?? string.Empty,
            Title = title,
            Description = description,
            EventTime = eventTime,
            EventType = eventType,
            RelatedCharacterIds = relatedCharacterIds
        };

        db.TimelineEvents.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok(JsonSerializer.Serialize(new
        {
            id = entity.Id,
            title = entity.Title,
            event_type = entity.EventType,
            event_time = entity.EventTime,
            message = $"时间线事件「{entity.Title}」已创建"
        }, options.Value));
    }
}
