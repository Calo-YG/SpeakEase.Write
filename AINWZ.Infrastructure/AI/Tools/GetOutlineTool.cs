using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetOutlineTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public GetOutlineTool(BlackboardHolder holder) => _holder = holder;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_outline",
            Description = "查询大纲结构，可按卷序、章序或关键词精确查询",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["volume_seq"] = new()
                    {
                        Type = "integer",
                        Description = "卷序号（可选）"
                    },
                    ["chapter_seq"] = new()
                    {
                        Type = "integer",
                        Description = "章节序号（可选，传入 volume_seq 时才生效）"
                    },
                    ["keyword"] = new()
                    {
                        Type = "string",
                        Description = "搜索关键词，按标题/目标/关键事件模糊匹配大纲节点（可选）"
                    }
                },
                Required = new List<string>()
            }
        }
    };

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var board = _holder.Blackboard;
        if (board?.Outline == null || (board.Outline.Volumes.Count == 0 && board.Outline.OutlineNodes.Count == 0))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "当前作品暂无大纲",
                ErrorCode = "no_outline"
            });
        }

        int? volumeSeq = null;
        int? chapterSeq = null;
        string keyword = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("volume_seq", out var vProp))
                volumeSeq = vProp.GetInt32();
            if (doc.RootElement.TryGetProperty("chapter_seq", out var cProp))
                chapterSeq = cProp.GetInt32();
            if (doc.RootElement.TryGetProperty("keyword", out var kProp))
                keyword = kProp.GetString();
        }
        catch
        {
        }

        if (volumeSeq.HasValue)
        {
            var volume = board.Outline.Volumes.FirstOrDefault(v => v.Sequence == volumeSeq.Value);
            if (volume == null)
                return Task.FromResult(ToolResult.Fail($"未找到第 {volumeSeq} 卷"));

            if (chapterSeq.HasValue)
            {
                var chapter = volume.Chapters.FirstOrDefault(c => c.Sequence == chapterSeq.Value);
                if (chapter == null)
                    return Task.FromResult(ToolResult.Fail($"未找到第 {volumeSeq} 卷第 {chapterSeq} 章"));
                return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(chapter)));
            }

            return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(volume)));
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            var matched = board.Outline.OutlineNodes
                .Where(n => n.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || n.Goal.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || n.KeyEvent.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.Sequence)
                .Take(15)
                .Select(n => new { n.Id, n.ParentId, n.Title, n.Goal, n.KeyEvent, n.Sequence, n.StageType })
                .ToList();
            if (matched.Count == 0)
                return Task.FromResult(ToolResult.Fail($"未找到匹配「{keyword}」的大纲节点"));
            return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(matched)));
        }

        return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(new
        {
            overall_arc = board.Outline.OverallArc,
            volumes = board.Outline.Volumes.Select(v => new
            {
                v.Sequence,
                v.Title,
                v.Summary,
                chapter_count = v.Chapters.Count,
                chapters = v.Chapters.Select(c => new { c.Sequence, c.Title, c.Summary, c.Status })
            }),
            outline_nodes = board.Outline.OutlineNodes
                .OrderBy(n => n.Sequence)
                .Select(n => new { n.Id, n.ParentId, n.Title, n.Goal, n.KeyEvent, n.Sequence, n.StageType })
        })));
    }
}
