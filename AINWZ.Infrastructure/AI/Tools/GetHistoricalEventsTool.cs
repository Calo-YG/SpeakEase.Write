using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 历史事件查询工具：查询世界历史事件，支持按时代标签和关键词筛选
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

        var query = db.HistoricalEvents.AsNoTracking().Where(e => e.WorkId == args.WorkId);

        if (!string.IsNullOrEmpty(args.EraLabel))
            query = query.Where(e => e.EraLabel == args.EraLabel);

        if (!string.IsNullOrEmpty(args.Keyword))
            query = query.Where(e =>
                (e.Title != null && e.Title.Contains(args.Keyword)) ||
                (e.Description != null && e.Description.Contains(args.Keyword)));

        var events = await query.OrderBy(e => e.EraLabel).ThenBy(e => e.EventTime).Take(100).ToListAsync(ct);

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

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string EraLabel { get; init; }
        public string Keyword { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            return null;
        }
    }
}
