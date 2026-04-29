using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetCharacterTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public GetCharacterTool(BlackboardHolder holder) => _holder = holder;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_character",
            Description = "根据角色名称查询角色的完整信息，包括性格、背景、说话风格、成长弧线等",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["name"] = new()
                    {
                        Type = "string",
                        Description = "角色名称"
                    }
                },
                Required = new List<string> { "name" }
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
            if (doc.RootElement.TryGetProperty("name", out var prop))
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
                Content = "缺少 name 参数",
                ErrorCode = "missing_parameter"
            });
        }

        var character = board.Characters.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? board.Characters.FirstOrDefault(c =>
                c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        if (character == null)
        {
            var names = string.Join("、", board.Characters.Select(c => c.Name));
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = $"未找到角色「{name}」，当前作品角色：{names}",
                ErrorCode = "character_not_found"
            });
        }

        var result = JsonSerializer.Serialize(new
        {
            character.Name,
            character.CoreSeed,
            character.Background,
            character.Personality,
            character.Traits,
            character.Voice,
            character.Arc,
            character.Relationships,
            character.Fears,
            character.Desires
        });

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = result
        });
    }
}
