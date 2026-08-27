using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 人物关系创建/更新工具：按角色名称匹配双方角色，自动查找已有关系并更新或新建
public sealed class CreateRelationshipTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_relationship",
            Description = "创建或更新两个人物之间的关系。按 source_name + target_name 匹配角色，自动查找已有关系并更新。关系类型建议: 父子/师徒/夫妻/宿敌/挚友/上下级/同门/恋人/仇人。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["source_name"] = new() { Type = "string", Description = "关系发起方角色名称（必填）" },
                    ["target_name"] = new() { Type = "string", Description = "关系目标方角色名称（必填）" },
                    ["relationship_type"] = new() { Type = "string", Description = "关系类型（必填），如: 父子/师徒/夫妻/宿敌/挚友/上下级/同门/恋人/仇人" },
                    ["description"] = new() { Type = "string", Description = "关系描述（可选），补充说明两人关系的具体情况" },
                    ["intensity"] = new() { Type = "integer", Description = "关系强度（可选，1-10，默认5），10为最强烈，如生死之交、刻骨仇恨等" }
                },
                Required = ["work_id", "source_name", "target_name", "relationship_type"]
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

        var intensity = args.Intensity.HasValue ? args.Intensity.Value : 5;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var source = await db.Characters.FirstOrDefaultAsync(
            c => c.WorkId == args.WorkId && c.Name == args.SourceName, ct)
            ?? await db.Characters.FirstOrDefaultAsync(
                c => c.WorkId == args.WorkId && c.Name != null && c.Name.Contains(args.SourceName), ct);

        var target = await db.Characters.FirstOrDefaultAsync(
            c => c.WorkId == args.WorkId && c.Name == args.TargetName, ct)
            ?? await db.Characters.FirstOrDefaultAsync(
                c => c.WorkId == args.WorkId && c.Name != null && c.Name.Contains(args.TargetName), ct);

        if (source == null)
            return ToolResult.Fail($"未找到角色「{args.SourceName}」", "source_not_found");
        if (target == null)
            return ToolResult.Fail($"未找到角色「{args.TargetName}」", "target_not_found");
        if (source.Id == target.Id)
            return ToolResult.Fail("不能为自己创建关系", "self_relationship");

        var existing = await db.CharacterRelationships.FirstOrDefaultAsync(
            r => r.WorkId == args.WorkId &&
                 r.SourceCharacterId == source.Id &&
                 r.TargetCharacterId == target.Id, ct);

        if (existing != null)
        {
            existing.RelationshipType = args.RelationshipType;
            if (args.Intensity.HasValue)
                existing.Intensity = intensity;
            if (!string.IsNullOrEmpty(args.Description))
                existing.Description = args.Description;
            existing.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"关系已更新: {source.Name} →[{args.RelationshipType}]→ {target.Name}，强度: {intensity}");
        }

        var entity = new CharacterRelationshipEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            SourceCharacterId = source.Id,
            TargetCharacterId = target.Id,
            RelationshipType = args.RelationshipType,
            Description = args.Description ?? string.Empty,
            Intensity = intensity
        };

        await db.CharacterRelationships.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"关系已创建: {source.Name} →[{args.RelationshipType}]→ {target.Name}，强度: {intensity}，ID: {entity.Id}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string SourceName { get; init; }
        public string TargetName { get; init; }
        public string RelationshipType { get; init; }
        public string Description { get; init; }
        public int? Intensity { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(SourceName))
                return ToolResult.Fail("缺少必需参数 'source_name'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(TargetName))
                return ToolResult.Fail("缺少必需参数 'target_name'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(RelationshipType))
                return ToolResult.Fail("缺少必需参数 'relationship_type'", "argument_parse_error");
            if (Intensity.HasValue && (Intensity.Value < 1 || Intensity.Value > 10))
                return ToolResult.Fail($"参数 'intensity' 值 {Intensity.Value} 超出范围 [1, 10]", "argument_parse_error");
            return null;
        }
    }
}
