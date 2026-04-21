using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using System.Text.Json;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 随机生成工具：支持随机整数、随机选择、骰子、UUID、列表打乱等
/// </summary>
public sealed class RandomGeneratorTool : IToolExecutor
{
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "random_generator",
            Description = "随机数与随机选择生成器，支持：随机整数、从列表中随机选择、掷骰子、生成UUID、打乱列表顺序",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["mode"] = new()
                    {
                        Type = "string",
                        Description = "生成模式：integer(随机整数)、choice(随机选择)、dice(掷骰子)、uuid(UUID)、shuffle(打乱列表)",
                        Enum = ["integer", "choice", "dice", "uuid", "shuffle"]
                    },
                    ["min"] = new()
                    {
                        Type = "integer",
                        Description = "随机整数最小值（mode=integer 时使用，默认1）"
                    },
                    ["max"] = new()
                    {
                        Type = "integer",
                        Description = "随机整数最大值（mode=integer 时使用，默认100）"
                    },
                    ["items"] = new()
                    {
                        Type = "array",
                        Description = "候选列表（mode=choice/shuffle 时使用）",
                        Items = new ParameterSchema { Type = "string" }
                    },
                    ["count"] = new()
                    {
                        Type = "integer",
                        Description = "掷骰子数量（mode=dice 时使用，默认1）"
                    },
                    ["sides"] = new()
                    {
                        Type = "integer",
                        Description = "骰子面数（mode=dice 时使用，默认6）"
                    }
                },
                Required = ["mode"]
            }
        }
    };

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        string mode = null;
        int min = 1, max = 100, count = 1, sides = 6;
        List<string> items = null;

        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            if (root.TryGetProperty("mode", out var modeProp))
                mode = modeProp.GetString();

            if (root.TryGetProperty("min", out var minProp))
                min = minProp.GetInt32();

            if (root.TryGetProperty("max", out var maxProp))
                max = maxProp.GetInt32();

            if (root.TryGetProperty("count", out var countProp))
                count = countProp.GetInt32();

            if (root.TryGetProperty("sides", out var sidesProp))
                sides = sidesProp.GetInt32();

            if (root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                items = itemsProp.EnumerateArray().Select(i => i.GetString() ?? "").ToList();
        }
        catch { /* 忽略 */ }

        if (string.IsNullOrEmpty(mode))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "缺少 mode 参数",
                ErrorCode = "missing_parameter"
            });
        }

        var random = Random.Shared;
        object resultObj;

        try
        {
            resultObj = mode switch
            {
                "integer" => new { mode, value = random.Next(min, max + 1), min, max },
                "choice" => items is not { Count: > 0 }
                    ? throw new ArgumentException("choice 模式需要提供 items 列表")
                    : new { mode, value = items[random.Next(items.Count)], from = items },
                "dice" => Enumerable.Range(0, Math.Max(1, count))
                    .Select(_ => random.Next(1, sides + 1)).ToArray() is var rolls
                    ? (object)new { mode, rolls, total = rolls.Sum(), count = rolls.Length, sides }
                    : new { mode },
                "uuid" => new { mode, value = Guid.NewGuid().ToString() },
                "shuffle" => items is not { Count: > 0 }
                    ? throw new ArgumentException("shuffle 模式需要提供 items 列表")
                    : new { mode, value = items.OrderBy(_ => random.Next()).ToList() },
                _ => throw new ArgumentException($"不支持的 mode: {mode}")
            };
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = ex.Message,
                ErrorCode = "invalid_parameter"
            });
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(resultObj)
        });
    }
}
