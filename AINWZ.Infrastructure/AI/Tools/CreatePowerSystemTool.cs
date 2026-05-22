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

public sealed class CreatePowerSystemTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_power_system",
            Description = "创建或更新力量体系/修炼体系条目，用于世界观构建。可存储修仙等级、武功境界、魔法体系等结构化定义。通过 id 或 name 查找已有体系，存在则更新提供的字段，不存在则创建。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "体系ID（可选），用于更新已有体系" },
                    ["name"] = new() { Type = "string", Description = "体系名称（必填），如: 修仙境界、武道等级" },
                    ["level_definition"] = new() { Type = "string", Description = "等级定义（新建必填，更新可选），JSON格式，如 {\"levels\":[\"炼气\",\"筑基\",\"金丹\",\"元婴\"]}" },
                    ["ability_rule"] = new() { Type = "string", Description = "能力规则（可选），如: 金丹期可御剑飞行" },
                    ["resource_system"] = new() { Type = "string", Description = "资源体系（可选），如: 灵石为通用货币" }
                },
                Required = ["work_id", "name"]
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

        PowerSystemEntity entity = null;
        if (!string.IsNullOrEmpty(args.Id))
            entity = await db.PowerSystems.FirstOrDefaultAsync(p => p.Id == args.Id && p.WorkId == args.WorkId, ct);
        if (entity == null)
            entity = await db.PowerSystems.FirstOrDefaultAsync(p => p.WorkId == args.WorkId && p.Name == args.Name, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(args.LevelDefinition)) entity.LevelDefinitionJson = args.LevelDefinition;
            if (args.AbilityRule != null) entity.AbilityRule = args.AbilityRule;
            if (args.ResourceSystem != null) entity.ResourceSystem = args.ResourceSystem;
            entity.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"力量体系「{entity.Name}」已更新，ID: {entity.Id}");
        }

        if (string.IsNullOrEmpty(args.LevelDefinition))
            return ToolResult.Fail("创建力量体系必须提供 level_definition");

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == args.WorkId, ct);

        var newEntity = new PowerSystemEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Name = args.Name,
            LevelDefinitionJson = args.LevelDefinition,
            AbilityRule = args.AbilityRule ?? string.Empty,
            ResourceSystem = args.ResourceSystem ?? string.Empty
        };

        await db.PowerSystems.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"力量体系「{args.Name}」已创建，ID: {newEntity.Id}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string Name { get; init; }
        public string LevelDefinition { get; init; }
        public string AbilityRule { get; init; }
        public string ResourceSystem { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Name))
                return ToolResult.Fail("缺少必需参数 'name'", "argument_parse_error");
            return null;
        }
    }
}
