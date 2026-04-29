using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetRecentChaptersTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public GetRecentChaptersTool(BlackboardHolder holder) => _holder = holder;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_recent_chapters",
            Description = "获取最近 N 章的内容，用于回顾前文",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["count"] = new()
                    {
                        Type = "integer",
                        Description = "需要获取的章节数量（默认 3）"
                    }
                },
                Required = new List<string>()
            }
        }
    };

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var board = _holder.Blackboard;
        if (board?.RecentChapters == null || board.RecentChapters.Count == 0)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "暂无已写章节",
                ErrorCode = "no_chapters"
            });
        }

        int count = 3;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("count", out var prop))
                count = prop.GetInt32();
        }
        catch
        {
        }

        if (count < 1) count = 1;
        if (count > 10) count = 10;

        var chapters = board.RecentChapters
            .OrderByDescending(c => c.Sequence)
            .Take(count)
            .OrderBy(c => c.Sequence)
            .ToList();

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(chapters.Select(c => new
            {
                c.Sequence,
                c.Title,
                c.Summary,
                c.Content
            }))
        });
    }
}
