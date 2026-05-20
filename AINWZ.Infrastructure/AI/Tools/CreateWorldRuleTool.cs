using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateWorldRuleTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_world_rule",
            Description = "创建或更新天道法则/世界限制机制，用于世界观构建。通过 id 或 rule_name 查找已有法则，存在则更新提供的字段，不存在则创建。rule_type 建议: 物理法则/天道规则/魔法法则/社会禁忌。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "法则ID（可选），用于更新已有法则" },
                    ["rule_name"] = new() { Type = "string", Description = "法则名称（必填），如: 灵气复苏、天劫机制" },
                    ["rule_type"] = new() { Type = "string", Description = "法则类型（新建必填，更新可选），如: 物理法则/天道规则/魔法法则/社会禁忌" },
                    ["description"] = new() { Type = "string", Description = "法则描述（新建必填，更新可选），详细说明该法则的内容与影响" },
                    ["constraint_json"] = new() { Type = "string", Description = "约束条件JSON（可选），结构化的限制条件" }
                },
                Required = ["work_id", "rule_name"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var id = args.GetString("id");
        var ruleName = args.GetString("rule_name", required: true);
        var ruleType = args.GetString("rule_type");
        var description = args.GetString("description");
        var constraintJson = args.GetString("constraint_json");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        WorldRuleEntity entity = null;
        if (!string.IsNullOrEmpty(id))
            entity = await db.WorldRules.FirstOrDefaultAsync(w => w.Id == id && w.WorkId == workId, ct);
        if (entity == null)
            entity = await db.WorldRules.FirstOrDefaultAsync(w => w.WorkId == workId && w.RuleName == ruleName, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(ruleType)) entity.RuleType = ruleType;
            if (description != null) entity.Description = description;
            if (constraintJson != null) entity.ConstraintJson = constraintJson;
            entity.UpdateAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"天道法则「{entity.RuleName}」（{entity.RuleType}）已更新，ID: {entity.Id}");
        }

        if (string.IsNullOrEmpty(ruleType))
            return ToolResult.Fail("创建法则必须提供 rule_type");
        if (string.IsNullOrEmpty(description))
            return ToolResult.Fail("创建法则必须提供 description");

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == workId, ct);

        var newEntity = new WorldRuleEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            RuleName = ruleName,
            RuleType = ruleType,
            Description = description,
            ConstraintJson = constraintJson ?? string.Empty
        };

        await db.WorldRules.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"天道法则「{ruleName}」（{ruleType}）已创建，ID: {newEntity.Id}");
    }
}
