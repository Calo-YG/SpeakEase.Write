using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 随机生成器工具，支持随机整数、随机选择、掷骰子、UUID 生成、随机打乱。
/// 适用于小说创作中的随机情节、掷骰判定、随机选择等场景。
/// </summary>
public sealed class RandomGeneratorTool:IToolExecutor
{
    private static readonly Random Random = Random.Shared;

    public static ToolDefinition Definition => new()
    {
        Type = "function",
        Function = new ToolFunctionDefinition
        {
            Name = "random_generator",
            Description = "随机生成器，支持随机整数(integer)、随机选择(pick)、掷骰子(dice)、UUID生成(uuid)、随机打乱(shuffle)五种模式。",
            Parameters = """
            {
                "type": "object",
                "properties": {
                    "mode": { "type": "string", "description": "模式: integer, pick, dice, uuid, shuffle", "enum": ["integer", "pick", "dice", "uuid", "shuffle"] },
                    "min": { "type": "integer", "description": "随机整数最小值，默认1" },
                    "max": { "type": "integer", "description": "随机整数最大值，默认100" },
                    "count": { "type": "integer", "description": "生成数量，默认1" },
                    "items": { "type": "array", "items": { "type": "string" }, "description": "pick/shuffle 模式的选项列表" },
                    "sides": { "type": "integer", "description": "骰子面数，默认6" },
                    "diceCount": { "type": "integer", "description": "骰子数量，默认1" },
                    "modifier": { "type": "integer", "description": "骰子修正值，默认0" }
                },
                "required": ["mode"]
            }
            """
        }
    };

    /// <summary>
    /// 
    /// </summary>
    public ToolDefinition ToolDefinition => Definition;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="arguments"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var input = JsonSerializer.Deserialize<RandomArguments>(arguments, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new RandomArguments();

        var mode = input.Mode?.ToLowerInvariant() ?? "integer";

        object result = mode switch
        {
            "integer" => GenerateInteger(input),
            "pick" => GeneratePick(input),
            "dice" => GenerateDice(input),
            "uuid" => GenerateUuid(),
            "shuffle" => GenerateShuffle(input),
            _ => Failure("unknown_mode", $"不支持的 mode: {mode}，可选: integer, pick, dice, uuid, shuffle")
        };

        if (result is ToolResult errorResult)
        {
            return Task.FromResult(errorResult);
        }

        var payload = JsonSerializer.Serialize(result);
        return Task.FromResult(new ToolResult
        {
            ToolName = "random_generator",
            Success = true,
            Content = payload
        });
    }

    private static object GenerateInteger(RandomArguments input)
    {
        var min = input.Min ?? 1;
        var max = input.Max ?? 100;
        if (min > max) (min, max) = (max, min);

        var count = Math.Clamp(input.Count ?? 1, 1, 100);
        var values = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(Random.Next(min, max + 1));
        }

        return new { mode = "integer", min, max, count, values };
    }

    private static object GeneratePick(RandomArguments input)
    {
        if (input.Items is null || input.Items.Count == 0)
        {
            return Failure("missing_options", "pick 模式需要提供 items 数组。");
        }

        var count = Math.Clamp(input.Count ?? 1, 1, input.Items.Count);
        var shuffled = input.Items.OrderBy(_ => Random.Next()).ToList();
        var picked = shuffled.Take(count).ToList();

        return new { mode = "pick", totalOptions = input.Items.Count, pickCount = count, picked };
    }

    private static object GenerateDice(RandomArguments input)
    {
        var diceCount = Math.Clamp(input.DiceCount ?? 1, 1, 100);
        var sides = Math.Clamp(input.Sides ?? 6, 2, 1000);
        var modifier = input.Modifier ?? 0;

        var rolls = new List<int>(diceCount);
        for (var i = 0; i < diceCount; i++)
        {
            rolls.Add(Random.Next(1, sides + 1));
        }

        var total = rolls.Sum() + modifier;

        return new
        {
            mode = "dice",
            notation = $"{diceCount}D{sides}" + (modifier != 0 ? (modifier > 0 ? $"+{modifier}" : modifier.ToString()) : ""),
            diceCount,
            sides,
            modifier,
            rolls,
            total
        };
    }

    private static object GenerateUuid()
    {
        return new
        {
            mode = "uuid",
            uuid = Guid.NewGuid().ToString(),
            uuidNoDash = Guid.NewGuid().ToString("N")
        };
    }

    private static object GenerateShuffle(RandomArguments input)
    {
        if (input.Items is null || input.Items.Count == 0)
        {
            return Failure("missing_options", "shuffle 模式需要提供 items 数组。");
        }

        var shuffled = input.Items.OrderBy(_ => Random.Next()).ToList();

        return new { mode = "shuffle", count = shuffled.Count, shuffled };
    }

    private static ToolResult Failure(string errorCode, string message)
    {
        return new ToolResult
        {
            ToolName = "random_generator",
            Success = false,
            ErrorCode = errorCode,
            Content = message
        };
    }

    /// <summary>
    /// 方法参数定义
    /// </summary>
    private sealed class RandomArguments
    {
        public string Mode { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }
        public int? Count { get; set; }
        public List<string> Items { get; set; }
        public int? Sides { get; set; }
        public int? DiceCount { get; set; }
        public int? Modifier { get; set; }
    }
}
