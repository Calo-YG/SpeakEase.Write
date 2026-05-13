using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class SearchWorldSettingTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
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
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["keyword"] = new() { Type = "string", Description = "搜索关键词（必填）" }
                },
                Required = ["work_id", "keyword"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var keyword = args.GetString("keyword", required: true);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var setting = await db.WorldSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId, ct);

        if (setting == null)
            return ToolResult.Fail("当前作品暂无世界观设定", "not_found");

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
            catch (JsonException) { }
        }

        if (parts.Count == 0)
            return ToolResult.Fail($"世界设定中未找到「{keyword}」相关内容", "no_matches");

        return ToolResult.Ok(string.Join("\n\n", parts));
    }
}
