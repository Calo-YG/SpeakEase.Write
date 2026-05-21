using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

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
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var sourceName = args.GetString("source_name", required: true);
        var targetName = args.GetString("target_name", required: true);
        var relType = args.GetString("relationship_type", required: true);
        var description = args.GetString("description");
        var intensity = args.GetInt32("intensity", defaultValue: 5, min: 1, max: 10);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var source = await db.Characters.FirstOrDefaultAsync(
            c => c.WorkId == workId && c.Name == sourceName, ct)
            ?? await db.Characters.FirstOrDefaultAsync(
                c => c.WorkId == workId && c.Name != null && c.Name.Contains(sourceName), ct);

        var target = await db.Characters.FirstOrDefaultAsync(
            c => c.WorkId == workId && c.Name == targetName, ct)
            ?? await db.Characters.FirstOrDefaultAsync(
                c => c.WorkId == workId && c.Name != null && c.Name.Contains(targetName), ct);

        if (source == null)
            return ToolResult.Fail($"未找到角色「{sourceName}」", "source_not_found");
        if (target == null)
            return ToolResult.Fail($"未找到角色「{targetName}」", "target_not_found");
        if (source.Id == target.Id)
            return ToolResult.Fail("不能为自己创建关系", "self_relationship");

        var existing = await db.CharacterRelationships.FirstOrDefaultAsync(
            r => r.WorkId == workId &&
                 r.SourceCharacterId == source.Id &&
                 r.TargetCharacterId == target.Id, ct);

        if (existing != null)
        {
            existing.RelationshipType = relType;
            if (args.Has("intensity"))
                existing.Intensity = intensity;
            if (!string.IsNullOrEmpty(description))
                existing.Description = description;
            existing.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"关系已更新: {source.Name} →[{relType}]→ {target.Name}，强度: {intensity}");
        }

        var entity = new CharacterRelationshipEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            SourceCharacterId = source.Id,
            TargetCharacterId = target.Id,
            RelationshipType = relType,
            Description = description ?? string.Empty,
            Intensity = intensity
        };

        await db.CharacterRelationships.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"关系已创建: {source.Name} →[{relType}]→ {target.Name}，强度: {intensity}，ID: {entity.Id}");
    }
}
