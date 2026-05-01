using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class SearchWorldSettingTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SearchWorldSettingTool(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "search_world_setting",
            Description = "按关键词在世界设定的摘要和Json内容中搜索",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["keyword"] = new() { Type = "string", Description = "搜索关键词" }
                },
                Required = new List<string> { "work_id", "keyword" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, keyword = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("keyword", out var k)) keyword = k.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (string.IsNullOrEmpty(keyword)) return ToolResult.Fail("缺少 keyword 参数");

        var setting = await db.WorldSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId, ct);

        if (setting == null)
            return ToolResult.Fail("当前作品暂无世界观设定");

        var parts = new List<string>();
        var keywords = keyword.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (!string.IsNullOrEmpty(setting.Summary))
        {
            var summary = setting.Summary.ToLowerInvariant();
            if (keywords.Any(k => summary.Contains(k)))
                parts.Add(string.Format("【摘要】{0}", setting.Summary));
        }

        if (!string.IsNullOrEmpty(setting.JsonContent))
        {
            try
            {
                var json = JsonSerializer.Deserialize<Dictionary<string, string>>(setting.JsonContent);
                if (json != null)
                {
                    foreach (var kv in json)
                    {
                        if (!string.IsNullOrEmpty(kv.Value) && keywords.Any(k => kv.Value.ToLowerInvariant().Contains(k)))
                            parts.Add(string.Format("【{0}】{1}", kv.Key, kv.Value));
                    }
                }
            }
            catch { }
        }

        if (parts.Count == 0)
            return ToolResult.Fail(string.Format("世界设定中未找到「{0}」相关内容", keyword));

        return ToolResult.Ok(string.Join("\n\n", parts));
    }
}
