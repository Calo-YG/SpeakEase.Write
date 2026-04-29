using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetForeshadowingTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public GetForeshadowingTool(BlackboardHolder holder) => _holder = holder;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_foreshadowing",
            Description = "查询伏笔列表，可按状态过滤（pending/resolved/paid_off），不传则返回全部",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["status"] = new()
                    {
                        Type = "string",
                        Description = "伏笔状态：pending（待回收）, resolved（已回收）, paid_off（已揭晓）"
                    }
                },
                Required = new List<string>()
            }
        }
    };

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var board = _holder.Blackboard;
        if (board?.AuditResults == null || board.AuditResults.Count == 0)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "当前作品暂无伏笔记录",
                ErrorCode = "no_foreshadowing"
            });
        }

        string status = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("status", out var prop))
                status = prop.GetString();
        }
        catch
        {
        }

        var results = board.AuditResults.AsEnumerable();

        if (!string.IsNullOrEmpty(status))
            results = results.Where(r => r.CheckType == status);

        var list = results.ToList();

        if (list.Count == 0)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = $"未找到状态为「{status ?? "任意"}」的伏笔",
                ErrorCode = "no_matches"
            });
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(list.Select(r => new
            {
                type = r.CheckType,
                severity = r.Severity,
                description = r.Description,
                suggestion = r.Suggestion
            }))
        });
    }
}
