using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateHistoricalEventTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_historical_event",
            Description = "创建或更新世界历史事件（背景历史，非故事剧情时间线）。通过 id 或 title 查找已有事件，存在则更新，不存在则创建。用于构建世界观的历史底蕴，如上古大战、王朝更替、灵气复苏等。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "事件ID（可选），用于更新已有事件" },
                    ["title"] = new() { Type = "string", Description = "事件标题（必填），如: 神魔大战、灵气复苏" },
                    ["description"] = new() { Type = "string", Description = "事件描述（新建必填，更新可选），详细说明事件经过" },
                    ["era_label"] = new() { Type = "string", Description = "时代标签（可选），如: 上古、中古、近世" },
                    ["event_time"] = new() { Type = "string", Description = "事件时间（可选），如: 万年前、三千年前" },
                    ["impact_summary"] = new() { Type = "string", Description = "影响概述（可选），该事件对世界格局的影响" }
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

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == args.WorkId, ct);

        HistoricalEventEntity entity = null;
        if (!string.IsNullOrEmpty(args.Id))
            entity = await db.HistoricalEvents.FirstOrDefaultAsync(e => e.Id == args.Id && e.WorkId == args.WorkId, ct);
        if (entity == null)
            entity = await db.HistoricalEvents.FirstOrDefaultAsync(e => e.WorkId == args.WorkId && e.Title == args.Title, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(args.Description)) entity.Description = args.Description;
            if (args.EraLabel != null) entity.EraLabel = args.EraLabel ?? string.Empty;
            if (args.EventTime != null) entity.EventTime = args.EventTime ?? string.Empty;
            if (args.ImpactSummary != null) entity.ImpactSummary = args.ImpactSummary ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"历史事件「{args.Title}」已更新，ID: {entity.Id}");
        }

        var newEntity = new HistoricalEventEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Title = args.Title,
            Description = args.Description ?? string.Empty,
            EraLabel = args.EraLabel ?? string.Empty,
            EventTime = args.EventTime ?? string.Empty,
            ImpactSummary = args.ImpactSummary ?? string.Empty
        };

        await db.HistoricalEvents.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"历史事件「{args.Title}」已创建，ID: {newEntity.Id}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string Title { get; init; }
        public string Description { get; init; }
        public string? EraLabel { get; init; }
        public string? EventTime { get; init; }
        public string? ImpactSummary { get; init; }

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
