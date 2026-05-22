using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var setting = await db.WorldSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == args.WorkId, ct);

        if (setting == null)
            return ToolResult.Fail("当前作品暂无世界观设定", "not_found");

        var parts = new List<string>();
        var keywords = args.Keyword.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

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
            return ToolResult.Fail($"世界设定中未找到「{args.Keyword}」相关内容", "no_matches");

        return ToolResult.Ok(string.Join("\n\n", parts));
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Keyword { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Keyword))
                return ToolResult.Fail("缺少必需参数 'keyword'", "argument_parse_error");
            return null;
        }
    }
}
