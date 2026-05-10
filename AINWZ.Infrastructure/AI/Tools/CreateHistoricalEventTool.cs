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
            Description = "创建世界历史事件（背景历史，非故事剧情时间线）。用于构建世界观的历史底蕴，如上古大战、王朝更替、灵气复苏等。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["title"] = new() { Type = "string", Description = "事件标题（必填），如: 神魔大战、灵气复苏" },
                    ["description"] = new() { Type = "string", Description = "事件描述（必填），详细说明事件经过" },
                    ["era_label"] = new() { Type = "string", Description = "时代标签（可选），如: 上古、中古、近世" },
                    ["event_time"] = new() { Type = "string", Description = "事件时间（可选），如: 万年前、三千年前" },
                    ["impact_summary"] = new() { Type = "string", Description = "影响概述（可选），该事件对世界格局的影响" }
                },
                Required = ["work_id", "title", "description"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var title = args.GetString("title", required: true);
        var description = args.GetString("description", required: true);
        var eraLabel = args.GetString("era_label");
        var eventTime = args.GetString("event_time");
        var impactSummary = args.GetString("impact_summary");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == workId, ct);

        var entity = new HistoricalEventEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Title = title,
            Description = description,
            EraLabel = eraLabel ?? string.Empty,
            EventTime = eventTime ?? string.Empty,
            ImpactSummary = impactSummary ?? string.Empty
        };

        await db.HistoricalEvents.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"历史事件「{title}」已创建，ID: {entity.Id}");
    }
}
