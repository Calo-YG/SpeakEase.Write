using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetForeshadowingTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GetForeshadowingTool(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Function = new FunctionDefinition
        {
            Name = "get_foreshadowing",
            Description = "查询伏笔列表，可按状态过滤（pending/active/hinted/resolved/paid_off）。",
            Parameters = new FunctionParameters
            {
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID" },
                    ["status"] = new()
                    {
                        Type = "string",
                        Description = "伏笔状态过滤（可选）",
                        Enum = new List<object> { "pending", "active", "hinted", "resolved", "paid_off" }
                    }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        string workId = null;
        string status = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (doc.RootElement.TryGetProperty("status", out var s)) status = s.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId))
            return new ToolResult { Success = false, Content = "缺少 work_id 参数", ErrorCode = "missing_parameter" };

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var query = db.Foreshadowings.AsNoTracking()
            .Where(x => x.WorkId == workId);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(x => x.Status == status);

        var foreshadowings = await query
            .OrderByDescending(x => x.Importance)
            .Take(50)
            .Select(x => new { x.Title, x.Description, x.Status, x.Importance, x.SetupChapterId })
            .ToListAsync(ct);

        if (foreshadowings.Count == 0)
            return new ToolResult { Content = "未找到伏笔" };

        var sb = new StringBuilder();
        sb.AppendLine($"找到 {foreshadowings.Count} 条伏笔：");
        foreach (var f in foreshadowings)
            sb.AppendLine($"[{f.Status ?? "未设置"}] {f.Title} — {f.Description} (重要性：{f.Importance}，来源：{f.SetupChapterId ?? "未知"})");

        return new ToolResult
        {
            Success = true,
            Content = sb.ToString()
        };
    }
}
