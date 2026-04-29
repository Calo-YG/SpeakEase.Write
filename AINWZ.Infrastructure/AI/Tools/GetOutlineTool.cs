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
            Description = "查询大纲结构，可按卷序和章序精确查询",
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
                    }
                },
                Required = new List<string>()
            }
        }
    };

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var board = _holder.Blackboard;
        if (board?.Outline == null || board.Outline.Volumes.Count == 0)
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
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("volume_seq", out var vProp))
                volumeSeq = vProp.GetInt32();
            if (doc.RootElement.TryGetProperty("chapter_seq", out var cProp))
                chapterSeq = cProp.GetInt32();
        }
        catch
        {
        }

        if (volumeSeq.HasValue)
        {
            var volume = board.Outline.Volumes.FirstOrDefault(v => v.Sequence == volumeSeq.Value);
            if (volume == null)
            {
                return Task.FromResult(new ToolResult
                {
                    Success = false,
                    Content = $"未找到第 {volumeSeq} 卷",
                    ErrorCode = "volume_not_found"
                });
            }

            if (chapterSeq.HasValue)
            {
                var chapter = volume.Chapters.FirstOrDefault(c => c.Sequence == chapterSeq.Value);
                if (chapter == null)
                {
                    return Task.FromResult(new ToolResult
                    {
                        Success = false,
                        Content = $"未找到第 {volumeSeq} 卷第 {chapterSeq} 章",
                        ErrorCode = "chapter_not_found"
                    });
                }
                return Task.FromResult(new ToolResult
                {
                    Success = true,
                    Content = JsonSerializer.Serialize(chapter)
                });
            }

            return Task.FromResult(new ToolResult
            {
                Success = true,
                Content = JsonSerializer.Serialize(volume)
            });
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(new
            {
                overall_arc = board.Outline.OverallArc,
                volumes = board.Outline.Volumes.Select(v => new
                {
                    v.Sequence,
                    v.Title,
                    v.Summary,
                    chapter_count = v.Chapters.Count
                })
            })
        });
    }
}
