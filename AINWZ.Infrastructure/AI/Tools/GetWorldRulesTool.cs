using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetWorldRulesTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_world_rules",
            Description = "查询作品的天道法则/世界限制机制。可按类型筛选或列出全部法则。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["rule_type"] = new() { Type = "string", Description = "法则类型筛选（可选），如: 物理法则/天道规则/魔法法则/社会禁忌" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var ruleType = args.GetString("rule_type");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var query = db.WorldRules.AsNoTracking().Where(r => r.WorkId == workId);

        if (!string.IsNullOrEmpty(ruleType))
            query = query.Where(r => r.RuleType == ruleType);

        var rules = await query.OrderBy(r => r.RuleName).Take(100).ToListAsync(ct);

        if (rules.Count == 0)
            return ToolResult.Fail(string.IsNullOrEmpty(ruleType)
                ? "当前作品暂无法则设定"
                : $"未找到类型为「{ruleType}」的法则", "not_found");

        var sb = new StringBuilder();
        sb.AppendLine($"## 天道法则（共{rules.Count}条）");

        foreach (var rule in rules)
        {
            sb.AppendLine($"\n### {rule.RuleName}（{rule.RuleType}）");
            sb.AppendLine(rule.Description);
            if (!string.IsNullOrEmpty(rule.ConstraintJson))
                sb.AppendLine($"约束条件：{rule.ConstraintJson}");
        }

        return ToolResult.Ok(sb.ToString());
    }
}
