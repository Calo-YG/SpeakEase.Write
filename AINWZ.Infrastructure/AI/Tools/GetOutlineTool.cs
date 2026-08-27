using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 大纲查询工具：返回大纲基本信息（标题/结构模板/摘要）和节点列表，支持按卷或关键词筛选
public sealed class GetOutlineTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_outline",
            Description = "查询大纲结构和节点。返回大纲基本信息（标题、结构模板、摘要）和节点列表。可通过 volume_seq 按卷查询章节分布，或用 keyword 搜索大纲节点。",
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

        var work = await db.Outlines.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == args.WorkId, ct);
        if (work == null)
            return ToolResult.Fail("未找到大纲", "not_found");

        var sb = new StringBuilder();
        sb.AppendLine($"大纲：{work.Title ?? "未设置"}");
        if (!string.IsNullOrEmpty(work.StructureTemplate))
            sb.AppendLine($"结构模板：{work.StructureTemplate}");
        sb.AppendLine($"走向：{work.Summary ?? "未设置"}");
        sb.AppendLine($"主大纲：{(work.IsPrimary ? "是" : "否")}");

        var volumes = await db.Volumes.AsNoTracking()
            .Where(x => x.WorkId == args.WorkId)
            .OrderBy(x => x.Sequence)
            .ToListAsync(ct);

        if (args.VolumeSeq > 0)
        {
            var vol = volumes.FirstOrDefault(v => v.Sequence == args.VolumeSeq);
            if (vol == null)
                return ToolResult.Fail($"未找到第{args.VolumeSeq}卷的大纲", "not_found");

            sb.AppendLine($"\n第{vol.Sequence}卷「{vol.Title}」：{vol.Summary ?? "无概述"}");

            var chapters = await db.Chapters.AsNoTracking()
                .Where(x => x.WorkId == args.WorkId && x.VolumeId == vol.Id)
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

        if (!string.IsNullOrEmpty(args.Keyword))
        {
            var nodes = await db.OutlineNodes.AsNoTracking()
                .Where(x => x.WorkId == args.WorkId &&
                    ((x.Title != null && x.Title.Contains(args.Keyword)) ||
                     (x.Goal != null && x.Goal.Contains(args.Keyword)) ||
                     (x.KeyEvent != null && x.KeyEvent.Contains(args.Keyword))))
                .Take(15)
                .Select(x => new { x.Title, x.Goal, x.KeyEvent, x.Sequence, x.StageType })
                .ToListAsync(ct);

            if (nodes.Count > 0)
            {
                sb.AppendLine($"\n关键词「{args.Keyword}」匹配的大纲节点：");
                foreach (var node in nodes)
                    sb.AppendLine($"[{node.StageType ?? "未指定"}] #{node.Sequence} {node.Title} — 目标：{node.Goal ?? "无"}；关键事件：{node.KeyEvent ?? "无"}");
            }
            else
            {
                sb.AppendLine($"\n关键词「{args.Keyword}」未匹配到大纲节点");
            }
        }
        else
        {
            var allNodes = await db.OutlineNodes.AsNoTracking()
                .Where(x => x.WorkId == args.WorkId)
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

    private sealed record Args
    {
        public string WorkId { get; init; }
        public int VolumeSeq { get; init; }
        public string Keyword { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (VolumeSeq != 0 && VolumeSeq < 1)
                return ToolResult.Fail($"参数 'volume_seq' 值 {VolumeSeq} 超出范围 [1, ∞]", "argument_parse_error");
            return null;
        }
    }
}
