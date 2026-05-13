using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class SearchCharactersTool(IServiceScopeFactory scopeFactory, IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
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
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["query"] = new() { Type = "string", Description = "搜索关键词（必填），如名称或身份" },
                    ["limit"] = new() { Type = "integer", Description = "返回数量上限（默认5，范围1-20）" }
                },
                Required = ["work_id", "query"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var query = args.GetString("query", required: true);
        var limit = args.GetInt32("limit", defaultValue: 5, min: 1, max: 20);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var matched = await db.Characters.AsNoTracking()
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
            return ToolResult.Fail($"未找到匹配「{query}」的角色", "no_matches");

        return ToolResult.Ok(JsonSerializer.Serialize(matched, snapshot.Value));
    }
}
