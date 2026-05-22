using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        TimelineEventEntity entity = null;
        if (!string.IsNullOrEmpty(args.Id))
            entity = await db.TimelineEvents.FirstOrDefaultAsync(e => e.Id == args.Id && e.WorkId == args.WorkId, ct);
        if (entity == null)
            entity = await db.TimelineEvents.FirstOrDefaultAsync(e => e.WorkId == args.WorkId && e.Title == args.Title, ct);

        if (entity != null)
        {
            if (args.Description != null) entity.Description = args.Description;
            if (!string.IsNullOrEmpty(args.EventTime))
                entity.EventTime = DateTime.TryParse(args.EventTime, out var dt) ? dt : DateTime.MinValue;
            if (!string.IsNullOrEmpty(args.EventType)) entity.EventType = args.EventType;
            if (args.ChapterId != null) entity.ChapterId = args.ChapterId;
            if (args.RelatedCharacterIds != null && args.RelatedCharacterIds.Count > 0) entity.RelatedCharacterIds = args.RelatedCharacterIds;
            entity.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"时间线事件「{entity.Title}」已更新，ID: {entity.Id}");
        }

        if (string.IsNullOrEmpty(args.Description))
            return ToolResult.Fail("创建事件必须提供 description");
        if (string.IsNullOrEmpty(args.EventTime))
            return ToolResult.Fail("创建事件必须提供 event_time");

        var newEntity = new TimelineEventEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            Title = args.Title,
            Description = args.Description,
            EventTime = DateTime.TryParse(args.EventTime, out var dt2) ? dt2 : DateTime.MinValue,
            EventType = args.EventType ?? "plot",
            ChapterId = args.ChapterId,
            RelatedCharacterIds = args.RelatedCharacterIds ?? []
        };

        await db.TimelineEvents.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"时间线事件「{args.Title}」已创建，ID: {newEntity.Id}，时间: {args.EventTime}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string Title { get; init; }
        public string Description { get; init; }
        public string EventTime { get; init; }
        public string EventType { get; init; }
        public string ChapterId { get; init; }
        public List<string> RelatedCharacterIds { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Title))
                return ToolResult.Fail("缺少必需参数 'title'", "argument_parse_error");
            return null;
        }
    }
}
