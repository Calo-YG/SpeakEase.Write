using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetOutlineTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_outline",
            Description = "查询大纲结构（总体走向、分卷安排、关键情节节点）。可通过 volume_seq 按卷查询，或用 keyword 按关键词搜索大纲节点。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["volume_seq"] = new() { Type = "integer", Description = "卷序号（可选，大于0）" },
                    ["keyword"] = new() { Type = "string", Description = "关键词（可选），在大纲节点的标题/目标/关键事件中搜索" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var volumeSeq = args.GetInt32("volume_seq", min: 0);
        var keyword = args.GetString("keyword");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var work = await db.Outlines.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId, ct);
        if (work == null)
            return ToolResult.Fail("未找到大纲", "not_found");

        var sb = new StringBuilder();
        sb.AppendLine($"大纲：{work.Title ?? "未设置"}");
        if (!string.IsNullOrEmpty(work.StructureTemplate))
            sb.AppendLine($"结构模板：{work.StructureTemplate}");
        sb.AppendLine($"走向：{work.Summary ?? "未设置"}");
        sb.AppendLine($"主大纲：{(work.IsPrimary ? "是" : "否")}");

        var volumes = await db.Volumes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(ct);

        if (volumeSeq > 0)
        {
            var vol = volumes.FirstOrDefault(v => v.Sequence == volumeSeq);
            if (vol == null)
                return ToolResult.Fail($"未找到第{volumeSeq}卷的大纲", "not_found");

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

        return ToolResult.Ok(sb.ToString());
    }
}
