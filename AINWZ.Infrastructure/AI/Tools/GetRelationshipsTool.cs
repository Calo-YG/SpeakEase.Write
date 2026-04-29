using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetRelationshipsTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public GetRelationshipsTool(BlackboardHolder holder) => _holder = holder;

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
                    ["character_name"] = new()
                    {
                        Type = "string",
                        Description = "角色名称"
                    }
                },
                Required = new List<string> { "character_name" }
            }
        }
    };

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var board = _holder.Blackboard;
        if (board?.Characters == null || board.Characters.Count == 0)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "当前作品暂无角色信息",
                ErrorCode = "no_characters"
            });
        }

        string name = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("character_name", out var prop))
                name = prop.GetString();
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(name))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "缺少 character_name 参数",
                ErrorCode = "missing_parameter"
            });
        }

        var character = board.Characters.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? board.Characters.FirstOrDefault(c =>
                c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        if (character == null)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = $"未找到角色「{name}」",
                ErrorCode = "character_not_found"
            });
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(new
            {
                character.Name,
                relationships = character.Relationships,
                fears = character.Fears,
                desires = character.Desires
            })
        });
    }
}
