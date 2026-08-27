using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 时间线事件查询工具：查询作品时间线事件，支持按 plot/character/world/backstory 类型过滤，按时间正序
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        var limit = args.Limit != 0 ? args.Limit : 20;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

        var query = db.TimelineEvents.AsNoTracking()
            .Where(x => x.WorkId == args.WorkId);

        if (!string.IsNullOrEmpty(args.EventType))
            query = query.Where(x => x.EventType == args.EventType);

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

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string EventType { get; init; }
        public int Limit { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (Limit != 0 && (Limit < 1 || Limit > 50))
                return ToolResult.Fail($"参数 'limit' 值 {Limit} 超出范围 [1, 50]", "argument_parse_error");
            return null;
        }
    }
}
