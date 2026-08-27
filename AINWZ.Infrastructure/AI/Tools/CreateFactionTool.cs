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

// 势力创建/更新工具：创建宗门/家族/帝国/组织等势力条目，包含势力间关系描述
public sealed class CreateFactionTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_faction",
            Description = "创建或更新势力条目（门派/家族/国家/组织），用于世界观构建。通过 id 或 name 查找已有势力，存在则更新，不存在则创建。faction_type 建议: 宗门/家族/帝国/商会/佣兵团/暗组织。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "势力ID（可选），用于更新已有势力" },
                    ["name"] = new() { Type = "string", Description = "势力名称（必填）" },
                    ["faction_type"] = new() { Type = "string", Description = "势力类型（新建必填，更新可选），如: 宗门/家族/帝国/商会/佣兵团/暗组织" },
                    ["description"] = new() { Type = "string", Description = "势力描述（新建必填，更新可选），包含历史、实力、特点等" },
                    ["relationship_json"] = new() { Type = "string", Description = "势力间关系描述（可选），如 \"与XX宗世代同盟，与YY门为敌对关系\"" }
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

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == args.WorkId, ct);

        FactionEntity entity = null;
        if (!string.IsNullOrEmpty(args.Id))
            entity = await db.Factions.FirstOrDefaultAsync(f => f.Id == args.Id && f.WorkId == args.WorkId, ct);
        if (entity == null)
            entity = await db.Factions.FirstOrDefaultAsync(f => f.WorkId == args.WorkId && f.Name == args.Name, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(args.FactionType)) entity.FactionType = args.FactionType;
            if (!string.IsNullOrEmpty(args.Description)) entity.Description = args.Description;
            if (args.RelationshipJson != null) entity.RelationshipJson = args.RelationshipJson ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"势力「{args.Name}」（{entity.FactionType}）已更新，ID: {entity.Id}");
        }

        var newEntity = new FactionEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Name = args.Name,
            FactionType = args.FactionType ?? string.Empty,
            Description = args.Description ?? string.Empty,
            RelationshipJson = args.RelationshipJson ?? string.Empty
        };

        await db.Factions.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"势力「{args.Name}」（{newEntity.FactionType}）已创建，ID: {newEntity.Id}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string Name { get; init; }
        public string FactionType { get; init; }
        public string Description { get; init; }
        public string RelationshipJson { get; init; }

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
