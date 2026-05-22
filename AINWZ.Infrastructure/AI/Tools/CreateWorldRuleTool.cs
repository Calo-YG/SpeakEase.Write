using System.Text.Json;
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        WorldRuleEntity entity = null;
        if (!string.IsNullOrEmpty(args.Id))
            entity = await db.WorldRules.FirstOrDefaultAsync(w => w.Id == args.Id && w.WorkId == args.WorkId, ct);
        if (entity == null)
            entity = await db.WorldRules.FirstOrDefaultAsync(w => w.WorkId == args.WorkId && w.RuleName == args.RuleName, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(args.RuleType)) entity.RuleType = args.RuleType;
            if (args.Description != null) entity.Description = args.Description;
            if (args.ConstraintJson != null) entity.ConstraintJson = args.ConstraintJson;
            entity.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"天道法则「{entity.RuleName}」（{entity.RuleType}）已更新，ID: {entity.Id}");
        }

        if (string.IsNullOrEmpty(args.RuleType))
            return ToolResult.Fail("创建法则必须提供 rule_type");
        if (string.IsNullOrEmpty(args.Description))
            return ToolResult.Fail("创建法则必须提供 description");

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == args.WorkId, ct);

        var newEntity = new WorldRuleEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            RuleName = args.RuleName,
            RuleType = args.RuleType,
            Description = args.Description,
            ConstraintJson = args.ConstraintJson ?? string.Empty
        };

        await db.WorldRules.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"天道法则「{args.RuleName}」（{args.RuleType}）已创建，ID: {newEntity.Id}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string RuleName { get; init; }
        public string RuleType { get; init; }
        public string Description { get; init; }
        public string ConstraintJson { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(RuleName))
                return ToolResult.Fail("缺少必需参数 'rule_name'", "argument_parse_error");
            return null;
        }
    }
}
