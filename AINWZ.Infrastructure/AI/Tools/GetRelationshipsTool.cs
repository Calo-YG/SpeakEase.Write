using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetRelationshipsTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GetRelationshipsTool(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_relationships",
            Description = "查询指定角色的人际关系网络",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new()
                    {
                        Type = "string",
                        Description = "作品ID"
                    },
                    ["character_name"] = new()
                    {
                        Type = "string",
                        Description = "角色名称"
                    }
                },
                Required = new List<string> { "work_id", "character_name" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        string workId = null;
        string name = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (doc.RootElement.TryGetProperty("character_name", out var prop))
                name = prop.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId))
            return new ToolResult { Success = false, Content = "缺少 work_id 参数", ErrorCode = "missing_parameter" };

        if (string.IsNullOrEmpty(name))
            return new ToolResult { Success = false, Content = "缺少 character_name 参数", ErrorCode = "missing_parameter" };

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var character = await db.Characters
            .FirstOrDefaultAsync(c => c.WorkId == workId && c.Name == name, ct)
            ?? await db.Characters.FirstOrDefaultAsync(c => c.WorkId == workId && c.Name != null && c.Name.Contains(name), ct);

        if (character == null)
            return new ToolResult { Success = false, Content = $"未找到角色「{name}」", ErrorCode = "character_not_found" };

        var relationships = await db.CharacterRelationships
            .Where(r => r.WorkId == workId && (r.SourceCharacterId == character.Id || r.TargetCharacterId == character.Id))
            .ToListAsync(ct);

        var relatedIds = relationships
            .SelectMany(r => new[] { r.SourceCharacterId, r.TargetCharacterId })
            .Where(id => id != character.Id && !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var relatedNames = new Dictionary<string, string>();
        if (relatedIds.Count > 0)
        {
            var chars = await db.Characters.AsNoTracking()
                .Where(c => relatedIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(ct);
            foreach (var c in chars)
                relatedNames[c.Id] = c.Name ?? c.Id;
        }

        var relList = new List<object>();
        foreach (var r in relationships)
        {
            var otherId = r.SourceCharacterId == character.Id ? r.TargetCharacterId : r.SourceCharacterId;
            var otherName = relatedNames.GetValueOrDefault(otherId ?? string.Empty, otherId ?? "未知");
            relList.Add(new
            {
                Target = otherName,
                r.RelationshipType,
                r.Description
            });
        }

        return new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(new
            {
                character.Name,
                Relationships = relList
            })
        };
    }
}
