using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetForeshadowingTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_foreshadowing",
            Description = "查询伏笔列表，可按状态过滤（pending/active/hinted/resolved/paid_off）。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["status"] = new()
                    {
                        Type = "string",
                        Description = "伏笔状态过滤（可选），枚举值: pending/active/hinted/resolved/paid_off",
                        Enum = new List<object> { "pending", "active", "hinted", "resolved", "paid_off" }
                    }
                },
                Required = ["work_id"]
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

        var query = db.Foreshadowings.AsNoTracking()
            .Where(x => x.WorkId == args.WorkId);
        if (!string.IsNullOrEmpty(args.Status))
            query = query.Where(x => x.Status == args.Status);

        var foreshadowings = await query
            .OrderByDescending(x => x.Importance)
            .Take(50)
            .Select(x => new { x.Title, x.Description, x.Status, x.Importance, x.SetupChapterId })
            .ToListAsync(ct);

        if (foreshadowings.Count == 0)
            return ToolResult.Ok("未找到伏笔");

        var sb = new StringBuilder();
        sb.AppendLine($"找到 {foreshadowings.Count} 条伏笔：");
        foreach (var f in foreshadowings)
            sb.AppendLine($"[{f.Status ?? "未设置"}] {f.Title} — {f.Description} (重要性：{f.Importance}，来源：{f.SetupChapterId ?? "未知"})");

        return ToolResult.Ok(sb.ToString());
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Status { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            return null;
        }
    }
}
