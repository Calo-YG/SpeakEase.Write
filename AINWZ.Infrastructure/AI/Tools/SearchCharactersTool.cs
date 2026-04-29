using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class SearchCharactersTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public SearchCharactersTool(BlackboardHolder holder) => _holder = holder;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "search_characters",
            Description = "模糊搜索角色，按名称/身份/标签匹配",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["query"] = new()
                    {
                        Type = "string",
                        Description = "搜索关键词，如名称或身份"
                    },
                    ["limit"] = new()
                    {
                        Type = "integer",
                        Description = "返回数量上限（默认 5）"
                    }
                },
                Required = new List<string> { "query" }
            }
        }
    };

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var board = _holder.Blackboard;
        if (board?.Characters == null || board.Characters.Count == 0)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "当前作品暂无角色信息",
                ErrorCode = "no_characters"
            });
        }

        string query = null;
        int limit = 5;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("query", out var qProp))
                query = qProp.GetString();
            if (doc.RootElement.TryGetProperty("limit", out var lProp))
                limit = lProp.GetInt32();
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(query))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "缺少 query 参数",
                ErrorCode = "missing_parameter"
            });
        }

        if (limit < 1) limit = 1;
        if (limit > 20) limit = 20;

        var results = board.Characters
            .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || c.CoreSeed.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || c.Background.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || c.Personality.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(c => new
            {
                c.CharacterId,
                c.Name,
                c.CoreSeed,
                c.Personality
            })
            .ToList();

        if (results.Count == 0)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = $"未找到匹配「{query}」的角色",
                ErrorCode = "no_matches"
            });
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(results)
        });
    }
}
