using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        var limit = args.Limit != 0 ? args.Limit : 5;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var matched = await db.Characters.AsNoTracking()
            .Where(c => c.WorkId == args.WorkId &&
                ((c.Name != null && c.Name.Contains(args.Query)) ||
                 (c.Identity != null && c.Identity.Contains(args.Query)) ||
                 (c.Personality != null && c.Personality.Contains(args.Query)) ||
                 (c.BackgroundStory != null && c.BackgroundStory.Contains(args.Query))))
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
            return ToolResult.Fail($"未找到匹配「{args.Query}」的角色", "no_matches");

        return ToolResult.Ok(JsonSerializer.Serialize(matched, snapshot.Value));
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Query { get; init; }
        public int Limit { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Query))
                return ToolResult.Fail("缺少必需参数 'query'", "argument_parse_error");
            if (Limit != 0 && (Limit < 1 || Limit > 20))
                return ToolResult.Fail($"参数 'limit' 值 {Limit} 超出范围 [1, 20]", "argument_parse_error");
            return null;
        }
    }
}
