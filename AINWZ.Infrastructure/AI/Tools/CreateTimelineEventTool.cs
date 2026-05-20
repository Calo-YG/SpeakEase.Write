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
            Description = "创建或更新当前作品的时间线事件，用于记录故事中发生的重要事件。事件按时间正序排列，可用于确保前后事件不冲突、维护时间脉络。通过 id 或 title 查找已有事件，存在则更新提供的字段，不存在则创建。event_type 枚举: plot/character/world/backstory。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "事件ID（可选），用于更新已有事件" },
                    ["title"] = new() { Type = "string", Description = "事件标题（必填）" },
                    ["description"] = new() { Type = "string", Description = "事件描述（新建必填，更新可选）" },
                    ["event_time"] = new() { Type = "string", Description = "事件发生时间（新建必填，更新可选），格式建议为故事内时间或相对标记（如'第一章末'、'大战后第三天'）" },
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
                Required = ["work_id", "title"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var id = args.GetString("id");
        var title = args.GetString("title", required: true);
        var description = args.GetString("description");
        var eventTime = args.GetString("event_time");
        var eventType = args.GetString("event_type") ?? "plot";
        var chapterId = args.GetString("chapter_id");
        var relatedCharacterIds = args.GetStringArray("related_character_ids");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        TimelineEventEntity entity = null;
        if (!string.IsNullOrEmpty(id))
            entity = await db.TimelineEvents.FirstOrDefaultAsync(e => e.Id == id && e.WorkId == workId, ct);
        if (entity == null)
            entity = await db.TimelineEvents.FirstOrDefaultAsync(e => e.WorkId == workId && e.Title == title, ct);

        if (entity != null)
        {
            if (description != null) entity.Description = description;
            if (!string.IsNullOrEmpty(eventTime))
                entity.EventTime = DateTime.TryParse(eventTime, out var dt) ? dt : DateTime.MinValue;
            if (!string.IsNullOrEmpty(eventType)) entity.EventType = eventType;
            if (chapterId != null) entity.ChapterId = chapterId;
            if (relatedCharacterIds.Count > 0) entity.RelatedCharacterIds = relatedCharacterIds;
            entity.UpdateAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"时间线事件「{entity.Title}」已更新，ID: {entity.Id}");
        }

        if (string.IsNullOrEmpty(description))
            return ToolResult.Fail("创建事件必须提供 description");
        if (string.IsNullOrEmpty(eventTime))
            return ToolResult.Fail("创建事件必须提供 event_time");

        var newEntity = new TimelineEventEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            Title = title,
            Description = description,
            EventTime = DateTime.TryParse(eventTime, out var dt2) ? dt2 : DateTime.MinValue,
            EventType = eventType,
            ChapterId = chapterId,
            RelatedCharacterIds = relatedCharacterIds
        };

        await db.TimelineEvents.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"时间线事件「{title}」已创建，ID: {newEntity.Id}，时间: {eventTime}");
    }
}
