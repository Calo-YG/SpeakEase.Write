using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class ResolveForeshadowingTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ResolveForeshadowingTool(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "resolve_foreshadowing",
            Description = "更新伏笔状态：标记为 hinted（已暗示）或 resolved（已回收），或 abandoned（已放弃）。回收时必须指定回收章节。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["foreshadowing_id"] = new() { Type = "string", Description = "伏笔标识" },
                    ["new_status"] = new() { Type = "string", Description = "新状态: hinted（已暗示）、resolved（已回收）、abandoned（已放弃）" },
                    ["payoff_chapter_id"] = new() { Type = "string", Description = "回收章节标识（状态为 resolved 时必填）" },
                    ["hint_detail"] = new() { Type = "string", Description = "本次暗示/回收的具体描述，写入伏笔描述追加" }
                },
                Required = new List<string> { "foreshadowing_id", "new_status" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string foreshadowingId = null, newStatus = null, payoffChapterId = null, hintDetail = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("foreshadowing_id", out var fi)) foreshadowingId = fi.GetString();
            if (root.TryGetProperty("new_status", out var ns)) newStatus = ns.GetString();
            if (root.TryGetProperty("payoff_chapter_id", out var pc)) payoffChapterId = pc.GetString();
            if (root.TryGetProperty("hint_detail", out var hd)) hintDetail = hd.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(foreshadowingId))
            return ToolResult.Fail("缺少 foreshadowing_id 参数");
        if (string.IsNullOrEmpty(newStatus))
            return ToolResult.Fail("缺少 new_status 参数");

        var allowedStatuses = new HashSet<string> { "hinted", "resolved", "abandoned" };
        newStatus = newStatus.ToLowerInvariant();
        if (!allowedStatuses.Contains(newStatus))
            return ToolResult.Fail($"无效状态「{newStatus}」，允许: hinted, resolved, abandoned");

        var entity = await db.Foreshadowings.FirstOrDefaultAsync(f => f.Id == foreshadowingId, ct);
        if (entity == null)
            return ToolResult.Fail($"未找到伏笔 {foreshadowingId}");

        if (newStatus == "resolved" && string.IsNullOrEmpty(payoffChapterId) && string.IsNullOrEmpty(entity.PayoffChapterId))
            return ToolResult.Fail("回收伏笔必须指定 payoff_chapter_id");

        var oldStatus = entity.Status;

        if (newStatus == "resolved" && !string.IsNullOrEmpty(payoffChapterId))
            entity.PayoffChapterId = payoffChapterId;

        if (!string.IsNullOrEmpty(hintDetail))
        {
            var existing = entity.Description ?? string.Empty;
            entity.Description = string.IsNullOrEmpty(existing)
                ? hintDetail
                : $"{existing}\n[{newStatus}] {hintDetail}";
        }

        entity.Status = newStatus;
        entity.UpdateAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok(JsonSerializer.Serialize(new
        {
            id = entity.Id,
            title = entity.Title,
            old_status = oldStatus,
            new_status = entity.Status,
            payoff_chapter_id = entity.PayoffChapterId,
            message = $"伏笔「{entity.Title}」状态已更新为 {entity.Status}"
        }));
    }
}
