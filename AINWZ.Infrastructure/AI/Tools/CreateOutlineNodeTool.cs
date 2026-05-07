using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateOutlineNodeTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_outline_node",
            Description = "创建一个新的大纲节点",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["parent_id"] = new() { Type = "string", Description = "父节点标识，根节点可留空" },
                    ["title"] = new() { Type = "string", Description = "节点标题" },
                    ["description"] = new() { Type = "string", Description = "节点描述/目标" },
                    ["key_event"] = new() { Type = "string", Description = "关键事件" },
                    ["sequence"] = new() { Type = "integer", Description = "排序序号" }
                },
                Required = ["work_id", "title"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, parentId = null, title = null, desc = null, keyEvent = null;
        int sequence = 0;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("parent_id", out var p)) parentId = p.GetString();
            if (root.TryGetProperty("title", out var t)) title = t.GetString();
            if (root.TryGetProperty("description", out var d)) desc = d.GetString();
            if (root.TryGetProperty("key_event", out var k)) keyEvent = k.GetString();
            if (root.TryGetProperty("sequence", out var s)) sequence = s.GetInt32();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (string.IsNullOrEmpty(title)) return ToolResult.Fail("缺少 title 参数");

        if (sequence <= 0)
        {
            var maxSeq = await db.OutlineNodes.AsNoTracking()
                .Where(x => x.WorkId == workId)
                .MaxAsync(x => (int?)x.Sequence, ct) ?? 0;
            sequence = maxSeq + 1;
        }

        var entity = new OutlineNodeEntity
        {
            Id = Guid.NewGuid().ToString(),
            WorkId = workId,
            ParentNodeId = parentId ?? string.Empty,
            Title = title,
            Goal = desc ?? string.Empty,
            KeyEvent = keyEvent ?? string.Empty,
            Sequence = sequence
        };

        db.OutlineNodes.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok(string.Format("大纲节点「{0}」已创建，id: {1}, sequence: {2}", title, entity.Id, sequence));
    }
}
