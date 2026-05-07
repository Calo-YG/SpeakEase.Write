using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class SearchCharactersTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
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
                    ["work_id"] = new()
                    {
                        Type = "string",
                        Description = "作品ID"
                    },
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
                Required = ["work_id", "query"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        string workId = null;
        string query = null;
        int limit = 5;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (doc.RootElement.TryGetProperty("query", out var qProp))
                query = qProp.GetString();
            if (doc.RootElement.TryGetProperty("limit", out var lProp))
                limit = lProp.GetInt32();
        }
        catch { }

        if (string.IsNullOrEmpty(workId))
            return new ToolResult { Success = false, Content = "缺少 work_id 参数", ErrorCode = "missing_parameter" };

        if (string.IsNullOrEmpty(query))
            return new ToolResult { Success = false, Content = "缺少 query 参数", ErrorCode = "missing_parameter" };

        if (limit < 1) limit = 1;
        if (limit > 20) limit = 20;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var matched = await db.Characters
            .Where(c => c.WorkId == workId &&
                ((c.Name != null && c.Name.Contains(query)) ||
                 (c.Identity != null && c.Identity.Contains(query)) ||
                 (c.Personality != null && c.Personality.Contains(query)) ||
                 (c.BackgroundStory != null && c.BackgroundStory.Contains(query))))
            .Take(limit)
            .Select(c => new
            {
                c.Id,
                c.Name,
                CoreSeed = c.Identity,
                c.Personality,
                Background = c.BackgroundStory ?? string.Empty
            })
            .ToListAsync(ct);

        if (matched.Count == 0)
            return new ToolResult { Success = false, Content = $"未找到匹配「{query}」的角色", ErrorCode = "no_matches" };

        return new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(matched)
        };
    }
}
