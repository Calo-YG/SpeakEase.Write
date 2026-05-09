using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetTimelineEventsTool(IServiceScopeFactory scopeFactory, IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
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
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["event_type"] = new()
                    {
                        Type = "string",
                        Description = "按事件类型过滤（可选），枚举值: plot/character/world/backstory",
                        Enum = new List<object> { "plot", "character", "world", "backstory" }
                    },
                    ["limit"] = new() { Type = "integer", Description = "返回数量上限（默认20，范围1-50）" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var eventType = args.GetString("event_type");
        var limit = args.GetInt32("limit", defaultValue: 20, min: 1, max: 50);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

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

        return ToolResult.Ok(JsonSerializer.Serialize<object>(events, snapshot.Value));
    }
}
