using System.Text.Json;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM.ToolHandlers;

/// <summary>
/// 随机生成器工具，支持随机整数、随机选择、掷骰子、UUID 生成。
/// 适用于小说创作中的随机情节、掷骰判定、随机选择等场景。
/// </summary>
public sealed class RandomGeneratorToolHandler : ILLMToolHandler
{
    private static readonly Random Random = Random.Shared;

    /// <inheritdoc />
    public string Name => "random_generator";

    /// <inheritdoc />
    public LLMToolDefinition ToolDefinition => new()
    {
        Type = "function",
        Function = new LLMToolFunctionDefinition
        {
            Name = Name,
            Description = "随机生成器，支持随机整数(integer)、随机选择(pick)、掷骰子(dice)、UUID生成(uuid)、随机打乱(shuffle)五种模式。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    mode = new { type = "string", description = "模式: integer, pick, dice, uuid, shuffle", @enum = new[] { "integer", "pick", "dice", "uuid", "shuffle" } },
                    min = new { type = "integer", description = "随机整数最小值，默认1" },
                    max = new { type = "integer", description = "随机整数最大值，默认100" },
                    count = new { type = "integer", description = "生成数量，默认1" },
                    items = new { type = "array", items = new { type = "string" }, description = "pick/shuffle 模式的选项列表" },
                    sides = new { type = "integer", description = "骰子面数，默认6" }
                },
                required = new[] { "mode" }
            }
        }
    };

    /// <inheritdoc />
    public Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
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

        if (result is LLMToolExecutionResult errorResult)
        {
            return Task.FromResult(errorResult);
        }

        var payload = JsonSerializer.Serialize(result);
        return Task.FromResult(new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            Content = payload
        });
    }

    /// <summary>
    /// 生成指定范围内的随机整数。
    /// </summary>
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

        return new
        {
            mode = "integer",
            min,
            max,
            count,
            values
        };
    }

    /// <summary>
    /// 从给定选项中随机选择。
    /// </summary>
    private static object GeneratePick(RandomArguments input)
    {
        if (input.Options is null || input.Options.Count == 0)
        {
            return Failure("missing_options", "pick 模式需要提供 options 数组。");
        }

        var count = Math.Clamp(input.Count ?? 1, 1, input.Options.Count);
        var shuffled = input.Options.OrderBy(_ => Random.Next()).ToList();
        var picked = shuffled.Take(count).ToList();

        return new
        {
            mode = "pick",
            totalOptions = input.Options.Count,
            pickCount = count,
            picked
        };
    }

    /// <summary>
    /// 掷骰子：支持 D4/D6/D8/D10/D12/D20/D100 及自定义面数。
    /// </summary>
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

    /// <summary>
    /// 生成 UUID v4。
    /// </summary>
    private static object GenerateUuid()
    {
        return new
        {
            mode = "uuid",
            uuid = Guid.NewGuid().ToString(),
            uuidNoDash = Guid.NewGuid().ToString("N")
        };
    }

    /// <summary>
    /// 随机打乱列表顺序。
    /// </summary>
    private static object GenerateShuffle(RandomArguments input)
    {
        if (input.Options is null || input.Options.Count == 0)
        {
            return Failure("missing_options", "shuffle 模式需要提供 options 数组。");
        }

        var shuffled = input.Options.OrderBy(_ => Random.Next()).ToList();

        return new
        {
            mode = "shuffle",
            count = shuffled.Count,
            shuffled
        };
    }

    private static LLMToolExecutionResult Failure(string errorCode, string message)
    {
        return new LLMToolExecutionResult
        {
            ToolName = "random_generator",
            Success = false,
            ErrorCode = errorCode,
            Content = message
        };
    }

    private sealed class RandomArguments
    {
        /// <summary>
        /// 生成模式: integer(默认), pick, dice, uuid, shuffle
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// 随机整数最小值（integer 模式）；默认 1。
        /// </summary>
        public int? Min { get; set; }

        /// <summary>
        /// 随机整数最大值（integer 模式）；默认 100。
        /// </summary>
        public int? Max { get; set; }

        /// <summary>
        /// 生成数量（integer/pick 模式）；默认 1。
        /// </summary>
        public int? Count { get; set; }

        /// <summary>
        /// 选项列表（pick/shuffle 模式）。
        /// </summary>
        public List<string> Options { get; set; }

        /// <summary>
        /// 骰子数量（dice 模式）；默认 1。
        /// </summary>
        public int? DiceCount { get; set; }

        /// <summary>
        /// 骰子面数（dice 模式）；默认 6。
        /// </summary>
        public int? Sides { get; set; }

        /// <summary>
        /// 修正值（dice 模式）；默认 0。
        /// </summary>
        public int? Modifier { get; set; }
    }
}
