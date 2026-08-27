using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 角色关系图谱查询工具：返回作品的角色关系网络，可按角色名称聚焦查看该角色的关系
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICharacterDbContext>();

        var characters = await db.Characters.AsNoTracking()
            .Where(c => c.WorkId == args.WorkId)
            .Select(c => new { c.Id, c.Name, c.Identity })
            .ToListAsync(ct);

        if (characters.Count == 0)
            return ToolResult.Fail("当前作品暂无角色", "no_characters");

        var characterMap = characters.ToDictionary(c => c.Id, c => c.Name ?? c.Id);

        var relationshipsQuery = db.CharacterRelationships.AsNoTracking()
            .Where(r => r.WorkId == args.WorkId);

        if (!string.IsNullOrEmpty(args.CharacterName))
        {
            var focusChar = characters.FirstOrDefault(c =>
                c.Name != null && c.Name.Contains(args.CharacterName));
            if (focusChar == null)
                return ToolResult.Fail($"未找到角色「{args.CharacterName}」", "not_found");

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

        if (!string.IsNullOrEmpty(args.CharacterName))
        {
            var focusChar = characters.First(c => c.Name != null && c.Name.Contains(args.CharacterName));
            var relatedIds = relationships
                .SelectMany(r => new[] { r.SourceCharacterId, r.TargetCharacterId })
                .Where(id => id != focusChar.Id)
                .Distinct()
                .ToList();

            var unrelated = characters.Where(c => c.Id != focusChar.Id && !relatedIds.Contains(c.Id)).ToList();
            if (unrelated.Count > 0)
            {
                sb.AppendLine($"\n与「{args.CharacterName}」暂无关系的角色: {string.Join("、", unrelated.Select(c => c.Name))}");
            }
        }

        return ToolResult.Ok(sb.ToString());
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string CharacterName { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            return null;
        }
    }
}
