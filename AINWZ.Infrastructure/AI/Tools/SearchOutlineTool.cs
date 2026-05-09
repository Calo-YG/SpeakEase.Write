using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class SearchOutlineTool(IServiceScopeFactory scopeFactory,IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
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
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["keyword"] = new() { Type = "string", Description = "搜索关键词" },
                    ["limit"] = new() { Type = "integer", Description = "返回数量上限（默认10）" }
                },
                Required = ["work_id", "keyword"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, keyword = null;
        int limit = 10;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("keyword", out var k)) keyword = k.GetString();
            if (root.TryGetProperty("limit", out var l)) limit = l.GetInt32();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (string.IsNullOrEmpty(keyword)) return ToolResult.Fail("缺少 keyword 参数");
        if (limit < 1) limit = 1;
        if (limit > 30) limit = 30;

        var nodes = await db.OutlineNodes.AsNoTracking()
            .Where(x => x.WorkId == workId
                && (x.Title.Contains(keyword) || x.Goal.Contains(keyword) || x.KeyEvent.Contains(keyword)))
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
            return ToolResult.Fail(string.Format("未找到匹配「{0}」的大纲节点", keyword));

        return ToolResult.Ok(JsonSerializer.Serialize(nodes,snapshot.Value));
    }
}
