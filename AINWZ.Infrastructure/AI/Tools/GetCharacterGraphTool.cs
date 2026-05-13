using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetCharacterGraphTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_character_graph",
            Description = "查询作品的人物关系图谱，返回所有角色之间的关系网络。可用于理解角色间的社会关系、排查关系矛盾、续写时保持关系一致性。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["character_name"] = new() { Type = "string", Description = "聚焦角色名称（可选），只返回该角色相关的关系" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var focusName = args.GetString("character_name");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var characters = await db.Characters.AsNoTracking()
            .Where(c => c.WorkId == workId)
            .Select(c => new { c.Id, c.Name, c.Identity })
            .ToListAsync(ct);

        if (characters.Count == 0)
            return ToolResult.Fail("当前作品暂无角色", "no_characters");

        var characterMap = characters.ToDictionary(c => c.Id, c => c.Name ?? c.Id);

        var relationshipsQuery = db.CharacterRelationships.AsNoTracking()
            .Where(r => r.WorkId == workId);

        if (!string.IsNullOrEmpty(focusName))
        {
            var focusChar = characters.FirstOrDefault(c =>
                c.Name != null && c.Name.Contains(focusName));
            if (focusChar == null)
                return ToolResult.Fail($"未找到角色「{focusName}」", "not_found");

            relationshipsQuery = relationshipsQuery.Where(r =>
                r.SourceCharacterId == focusChar.Id || r.TargetCharacterId == focusChar.Id);
        }

        var relationships = await relationshipsQuery.OrderBy(r => r.RelationshipType).Take(200).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine($"## 人物关系图谱（{characters.Count}个角色，{relationships.Count}条关系）");
        sb.AppendLine();

        if (relationships.Count == 0)
        {
            sb.AppendLine("暂无人物关系记录");
        }
        else
        {
            foreach (var rel in relationships)
            {
                var sourceName = characterMap.GetValueOrDefault(rel.SourceCharacterId, rel.SourceCharacterId);
                var targetName = characterMap.GetValueOrDefault(rel.TargetCharacterId, rel.TargetCharacterId);
                sb.AppendLine($"  {sourceName} →[{rel.RelationshipType}]→ {targetName}（强度：{rel.Intensity}）");
                if (!string.IsNullOrEmpty(rel.Description))
                    sb.AppendLine($"    备注: {rel.Description}");
            }
        }

        if (!string.IsNullOrEmpty(focusName))
        {
            var focusChar = characters.First(c => c.Name != null && c.Name.Contains(focusName));
            var relatedIds = relationships
                .SelectMany(r => new[] { r.SourceCharacterId, r.TargetCharacterId })
                .Where(id => id != focusChar.Id)
                .Distinct()
                .ToList();

            var unrelated = characters.Where(c => c.Id != focusChar.Id && !relatedIds.Contains(c.Id)).ToList();
            if (unrelated.Count > 0)
            {
                sb.AppendLine($"\n与「{focusName}」暂无关系的角色: {string.Join("、", unrelated.Select(c => c.Name))}");
            }
        }

        return ToolResult.Ok(sb.ToString());
    }
}
