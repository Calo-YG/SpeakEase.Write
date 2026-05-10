using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetHistoricalEventsTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_historical_events",
            Description = "查询作品的世界历史事件（背景历史，非故事剧情时间线）。可按时代标签筛选或列出全部历史事件。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["era_label"] = new() { Type = "string", Description = "时代标签筛选（可选），如: 上古、中古" },
                    ["keyword"] = new() { Type = "string", Description = "关键词搜索（可选），在标题和描述中搜索" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var eraLabel = args.GetString("era_label");
        var keyword = args.GetString("keyword");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var query = db.HistoricalEvents.AsNoTracking().Where(e => e.WorkId == workId);

        if (!string.IsNullOrEmpty(eraLabel))
            query = query.Where(e => e.EraLabel == eraLabel);

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(e =>
                (e.Title != null && e.Title.Contains(keyword)) ||
                (e.Description != null && e.Description.Contains(keyword)));

        var events = await query.ToListAsync(ct);

        if (events.Count == 0)
            return ToolResult.Fail("当前作品暂无历史事件", "not_found");

        var sb = new StringBuilder();
        sb.AppendLine($"## 世界历史（共{events.Count}个事件）");

        foreach (var evt in events)
        {
            sb.AppendLine($"\n### {evt.Title}");
            if (!string.IsNullOrEmpty(evt.EraLabel) || !string.IsNullOrEmpty(evt.EventTime))
                sb.AppendLine($"时间：{evt.EraLabel} {evt.EventTime}");
            sb.AppendLine(evt.Description);
            if (!string.IsNullOrEmpty(evt.ImpactSummary))
                sb.AppendLine($"影响：{evt.ImpactSummary}");
        }

        return ToolResult.Ok(sb.ToString());
    }
}
