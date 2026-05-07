using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetOutlineTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GetOutlineTool(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Function = new FunctionDefinition
        {
            Name = "get_outline",
            Description = "查询大纲结构（总体走向、分卷安排、关键情节节点）。可通过 volume_seq 按卷查询，或用 keyword 按关键词搜索大纲节点。",
            Parameters = new FunctionParameters
            {
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID" },
                    ["volume_seq"] = new() { Type = "integer", Description = "卷序号（可选）" },
                    ["keyword"] = new() { Type = "string", Description = "关键词（可选），在大纲节点的标题/目标/关键事件中搜索" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        string workId = null;
        int? volumeSeq = null;
        string keyword = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (doc.RootElement.TryGetProperty("volume_seq", out var v) && v.ValueKind == JsonValueKind.Number)
                volumeSeq = v.GetInt32();
            if (doc.RootElement.TryGetProperty("keyword", out var k)) keyword = k.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId))
            return new ToolResult { Success = false, Content = "缺少 work_id 参数", ErrorCode = "missing_parameter" };

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var work = await db.Outlines.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId, ct);
        if (work == null)
            return new ToolResult { Content = "未找到大纲" };

        var sb = new StringBuilder();
        sb.AppendLine($"大纲：{work.Title ?? "未设置"}");
        sb.AppendLine($"走向：{work.Summary ?? "未设置"}");

        var volumes = await db.Volumes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(ct);

        if (volumeSeq.HasValue)
        {
            var vol = volumes.FirstOrDefault(v => v.Sequence == volumeSeq.Value);
            if (vol == null)
                return new ToolResult { Content = $"未找到第{volumeSeq}卷的大纲" };

            sb.AppendLine($"\n第{vol.Sequence}卷「{vol.Title}」：{vol.Summary ?? "无概述"}");

            var chapters = await db.Chapters.AsNoTracking()
                .Where(x => x.WorkId == workId && x.VolumeId == vol.Id)
                .OrderBy(x => x.Sequence)
                .Select(x => new { x.Sequence, x.Title, x.Summary })
                .ToListAsync(ct);

            foreach (var ch in chapters)
                sb.AppendLine($"  第{ch.Sequence}章「{ch.Title}」：{ch.Summary ?? "无概述"}");
        }
        else
        {
            foreach (var vol in volumes)
                sb.AppendLine($"第{vol.Sequence}卷「{vol.Title}」：{vol.Summary ?? "无概述"}");
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            var nodes = await db.OutlineNodes.AsNoTracking()
                .Where(x => x.WorkId == workId &&
                    ((x.Title != null && x.Title.Contains(keyword)) ||
                     (x.Goal != null && x.Goal.Contains(keyword)) ||
                     (x.KeyEvent != null && x.KeyEvent.Contains(keyword))))
                .Take(15)
                .Select(x => new { x.Title, x.Goal, x.KeyEvent, x.Sequence, x.StageType })
                .ToListAsync(ct);

            if (nodes.Count > 0)
            {
                sb.AppendLine($"\n关键词「{keyword}」匹配的大纲节点：");
                foreach (var node in nodes)
                    sb.AppendLine($"[{node.StageType ?? "未指定"}] #{node.Sequence} {node.Title} — 目标：{node.Goal ?? "无"}；关键事件：{node.KeyEvent ?? "无"}");
            }
            else
            {
                sb.AppendLine($"\n关键词「{keyword}」未匹配到大纲节点");
            }
        }
        else
        {
            var allNodes = await db.OutlineNodes.AsNoTracking()
                .Where(x => x.WorkId == workId)
                .OrderBy(x => x.Sequence)
                .Take(50)
                .Select(x => new { x.Title, x.Goal, x.KeyEvent, x.Sequence, x.StageType })
                .ToListAsync(ct);

            if (allNodes.Count > 0)
            {
                sb.AppendLine("\n大纲节点：");
                foreach (var node in allNodes)
                    sb.AppendLine($"[{node.StageType ?? "未指定"}] #{node.Sequence} {node.Title} — 目标：{node.Goal ?? "无"}；关键事件：{node.KeyEvent ?? "无"}");
            }
        }

        return new ToolResult
        {
            Success = true,
            Content = sb.ToString()
        };
    }
}
