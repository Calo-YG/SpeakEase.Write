using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 大纲搜索工具：按关键词在大纲节点的标题/目标/关键事件中搜索匹配节点
public sealed class SearchOutlineTool(IServiceScopeFactory scopeFactory, IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "search_outline",
            Description = "按关键词搜索大纲节点",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["keyword"] = new() { Type = "string", Description = "搜索关键词（必填）" },
                    ["limit"] = new() { Type = "integer", Description = "返回数量上限（默认10，范围1-30）" }
                },
                Required = ["work_id", "keyword"]
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

        var limit = args.Limit != 0 ? args.Limit : 10;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IStoryDbContext>();

        var nodes = await db.OutlineNodes.AsNoTracking()
            .Where(x => x.WorkId == args.WorkId
                && (x.Title.Contains(args.Keyword) || x.Goal.Contains(args.Keyword) || x.KeyEvent.Contains(args.Keyword)))
            .OrderBy(x => x.Sequence)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.ParentNodeId,
                x.Title,
                x.Goal,
                x.KeyEvent,
                x.Sequence,
                x.StageType
            })
            .ToListAsync(ct);

        if (nodes.Count == 0)
            return ToolResult.Fail($"未找到匹配「{args.Keyword}」的大纲节点", "no_matches");

        return ToolResult.Ok(JsonSerializer.Serialize(nodes, snapshot.Value));
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Keyword { get; init; }
        public int Limit { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Keyword))
                return ToolResult.Fail("缺少必需参数 'keyword'", "argument_parse_error");
            if (Limit != 0 && (Limit < 1 || Limit > 30))
                return ToolResult.Fail($"参数 'limit' 值 {Limit} 超出范围 [1, 30]", "argument_parse_error");
            return null;
        }
    }
}
