using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetChapterTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public GetChapterTool(BlackboardHolder holder) => _holder = holder;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_chapter",
            Description = "根据章节 ID 获取特定章节的完整内容",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["chapter_id"] = new()
                    {
                        Type = "string",
                        Description = "章节标识"
                    }
                },
                Required = new List<string> { "chapter_id" }
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

        string chapterId = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("chapter_id", out var prop))
                chapterId = prop.GetString();
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(chapterId))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "缺少 chapter_id 参数",
                ErrorCode = "missing_parameter"
            });
        }

        var chapter = board.RecentChapters.FirstOrDefault(c => c.ChapterId == chapterId)
                      ?? board.RecentChapters.FirstOrDefault(c =>
                          c.ChapterId.Contains(chapterId, StringComparison.OrdinalIgnoreCase));

        if (chapter == null)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = $"未找到章节 {chapterId}",
                ErrorCode = "chapter_not_found"
            });
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(new
            {
                chapter.ChapterId,
                chapter.Sequence,
                chapter.Title,
                chapter.Summary,
                chapter.Content
            })
        });
    }
}
