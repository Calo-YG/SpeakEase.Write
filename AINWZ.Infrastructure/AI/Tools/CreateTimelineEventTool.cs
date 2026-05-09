using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateTimelineEventTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_timeline_event",
            Description = "为当前作品创建时间线事件，用于记录故事中发生的重要事件。事件按时间正序排列，可用于确保前后事件不冲突、维护时间脉络。event_type 枚举: plot/character/world/backstory。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["title"] = new() { Type = "string", Description = "事件标题（必填）" },
                    ["description"] = new() { Type = "string", Description = "事件描述（必填）" },
                    ["event_time"] = new() { Type = "string", Description = "事件发生时间（必填），格式建议为故事内时间或相对标记（如“第一章末”、“大战后第三天”）" },
                    ["event_type"] = new()
                    {
                        Type = "string",
                        Description = "事件类型（可选，默认 plot），枚举值: plot=情节、character=角色相关、world=世界设定、backstory=前史/回忆",
                        Enum = new List<object> { "plot", "character", "world", "backstory" }
                    },
                    ["chapter_id"] = new() { Type = "string", Description = "关联章节标识（可选）" },
                    ["related_character_ids"] = new()
                    {
                        Type = "array",
                        Items = new ParameterSchema { Type = "string" },
                        Description = "关联角色标识列表（可选），数组格式"
                    }
                },
                Required = ["work_id", "title", "description", "event_time"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var title = args.GetString("title", required: true);
        var description = args.GetString("description", required: true);
        var eventTime = args.GetString("event_time", required: true);
        var eventType = args.GetString("event_type") ?? "plot";
        var chapterId = args.GetString("chapter_id");
        var relatedCharacterIds = args.GetStringArray("related_character_ids");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var entity = new TimelineEventEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            Title = title,
            Description = description,
            EventTime = DateTime.TryParse(eventTime, out var dt) ? dt : DateTime.UtcNow,
            EventType = eventType,
            ChapterId = chapterId,
            RelatedCharacterIds = relatedCharacterIds
        };

        await db.TimelineEvents.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"时间线事件「{title}」已创建，ID: {entity.Id}，时间: {eventTime}");
    }
}
