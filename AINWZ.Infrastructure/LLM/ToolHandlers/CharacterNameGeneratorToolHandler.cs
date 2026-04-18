using System.Text.Json;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM.ToolHandlers;

/// <summary>
/// 角色姓名生成器，支持中文姓名、英文名、奇幻风格名、日文名等多种风格。
/// 适用于小说创作中的角色命名需求。
/// </summary>
public sealed class CharacterNameGeneratorToolHandler : ILLMToolHandler
{
    private static readonly Random Random = Random.Shared;

    /// <inheritdoc />
    public string Name => "generate_character_name";

    /// <inheritdoc />
    public LLMToolDefinition ToolDefinition => new()
    {
        Type = "function",
        Function = new LLMToolFunctionDefinition
        {
            Name = Name,
            Description = "角色姓名生成器，支持中文(chinese)、英文(english)、奇幻(fantasy)、日文(japanese)四种风格。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    style = new { type = "string", description = "风格: chinese, english, fantasy, japanese", @enum = new[] { "chinese", "english", "fantasy", "japanese" } },
                    gender = new { type = "string", description = "性别: male, female", @enum = new[] { "male", "female" } },
                    count = new { type = "integer", description = "生成数量，默认1，上限20" }
                },
                required = new[] { "style" }
            }
        }
    };

    // 常见中文姓氏（百家姓前100）
    private static readonly string[] ChineseSurnames =
    [
        "赵", "钱", "孙", "李", "周", "吴", "郑", "王", "冯", "陈",
        "褚", "卫", "蒋", "沈", "韩", "杨", "朱", "秦", "尤", "许",
        "何", "吕", "施", "张", "孔", "曹", "严", "华", "金", "魏",
        "陶", "姜", "戚", "谢", "邹", "喻", "柏", "水", "窦", "章",
        "云", "苏", "潘", "葛", "奚", "范", "彭", "郎", "鲁", "韦",
        "昌", "马", "苗", "凤", "花", "方", "俞", "任", "袁", "柳",
        "酆", "鲍", "史", "唐", "费", "廉", "岑", "薛", "雷", "贺",
        "倪", "汤", "滕", "殷", "罗", "毕", "郝", "邬", "安", "常",
        "乐", "于", "时", "傅", "皮", "卞", "齐", "康", "伍", "余",
        "元", "卜", "顾", "孟", "平", "黄", "和", "穆", "萧", "尹"
    ];

    // 中文男性名字用字
    private static readonly string[] ChineseMaleChars =
    [
        "伟", "刚", "勇", "毅", "俊", "峰", "强", "军", "平", "保",
        "东", "文", "辉", "力", "明", "永", "健", "世", "广", "志",
        "义", "兴", "良", "海", "山", "仁", "波", "宁", "贵", "福",
        "生", "龙", "元", "全", "国", "胜", "学", "祥", "才", "发",
        "武", "新", "利", "清", "飞", "彬", "富", "顺", "信", "子"
    ];

    // 中文女性名字用字
    private static readonly string[] ChineseFemaleChars =
    [
        "秀", "娟", "华", "慧", "巧", "美", "娜", "静", "淑", "惠",
        "珠", "翠", "雅", "芝", "玉", "萍", "红", "娥", "玲", "芬",
        "芳", "燕", "彩", "春", "菊", "兰", "凤", "洁", "梅", "琳",
        "素", "云", "莲", "真", "环", "雪", "荣", "爱", "妹", "霞",
        "香", "月", "莺", "媛", "艳", "瑞", "凡", "枫", "萱", "珺"
    ];

    // 英文男性名
    private static readonly string[] EnglishMaleNames =
    [
        "James", "John", "Robert", "Michael", "William", "David", "Richard",
        "Joseph", "Thomas", "Charles", "Christopher", "Daniel", "Matthew",
        "Anthony", "Mark", "Donald", "Steven", "Andrew", "Paul", "Joshua"
    ];

    // 英文女性名
    private static readonly string[] EnglishFemaleNames =
    [
        "Mary", "Patricia", "Jennifer", "Linda", "Barbara", "Elizabeth",
        "Susan", "Jessica", "Sarah", "Karen", "Lisa", "Nancy", "Betty",
        "Margaret", "Sandra", "Ashley", "Dorothy", "Kimberly", "Emily", "Donna"
    ];

    // 英文姓氏
    private static readonly string[] EnglishSurnames =
    [
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller",
        "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez",
        "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin"
    ];

    // 奇幻风格前缀
    private static readonly string[] FantasyPrefixes =
    [
        "Ael", "Bal", "Cel", "Dra", "Eld", "Fal", "Gor", "Hal", "Ith", "Jar",
        "Kael", "Lor", "Myr", "Nar", "Ori", "Pel", "Quin", "Rav", "Syl", "Thr"
    ];

    // 奇幻风格后缀
    private static readonly string[] FantasySuffixes =
    [
        "ion", "ius", "ara", "eth", "orn", "ain", "iel", "orn", "ric", "ius",
        "wen", "mir", "ath", "on", "iel", "dan", "rok", "ven", "ath", "ion"
    ];

    // 日文姓氏
    private static readonly string[] JapaneseSurnames =
    [
        "佐藤", "鈴木", "高橋", "田中", "渡辺", "伊藤", "山本", "中村",
        "小林", "加藤", "吉田", "山田", "佐々木", "山口", "松本", "井上",
        "木村", "林", "斎藤", "清水"
    ];

    // 日文男性名
    private static readonly string[] JapaneseMaleNames =
    [
        "翔", "蓮", "樹", "蒼", "陽翔", "大翔", "悠真", "颯", "陽太", "悠",
        "健", "誠", "大輝", "翼", "颯太", "拓海", "陽斗", "蓮", "蒼", "樹"
    ];

    // 日文女性名
    private static readonly string[] JapaneseFemaleNames =
    [
        "結愛", "陽菜", "咲", "芽依", "結衣", "心春", "莉子", "琴",
        "桜", "杏", "美咲", "花", "結", "葵", "柚希", "美優", "桃花", "雫"
    ];

    /// <inheritdoc />
    public Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var input = JsonSerializer.Deserialize<NameArguments>(arguments, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new NameArguments();

        var style = input.Style?.ToLowerInvariant() ?? "chinese";
        var gender = input.Gender?.ToLowerInvariant() ?? "male";
        var count = Math.Clamp(input.Count ?? 3, 1, 20);

        var names = style switch
        {
            "chinese" => GenerateChineseNames(gender, count),
            "english" => GenerateEnglishNames(gender, count),
            "fantasy" => GenerateFantasyNames(gender, count),
            "japanese" => GenerateJapaneseNames(gender, count),
            _ => null
        };

        if (names is null)
        {
            return Task.FromResult(Failure("unknown_style", $"不支持的 style: {style}，可选: chinese, english, fantasy, japanese"));
        }

        var payload = JsonSerializer.Serialize(new
        {
            style,
            gender,
            count,
            names
        });

        return Task.FromResult(new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            Content = payload
        });
    }

    private static List<string> GenerateChineseNames(string gender, int count)
    {
        var names = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var surname = ChineseSurnames[Random.Next(ChineseSurnames.Length)];
            var charPool = gender == "female" ? ChineseFemaleChars : ChineseMaleChars;

            // 1字名 or 2字名 随机
            if (Random.Next(3) == 0)
            {
                names.Add(surname + charPool[Random.Next(charPool.Length)]);
            }
            else
            {
                names.Add(surname + charPool[Random.Next(charPool.Length)] + charPool[Random.Next(charPool.Length)]);
            }
        }
        return names;
    }

    private static List<string> GenerateEnglishNames(string gender, int count)
    {
        var names = new List<string>(count);
        var givenNames = gender == "female" ? EnglishFemaleNames : EnglishMaleNames;
        for (var i = 0; i < count; i++)
        {
            var given = givenNames[Random.Next(givenNames.Length)];
            var surname = EnglishSurnames[Random.Next(EnglishSurnames.Length)];
            names.Add($"{given} {surname}");
        }
        return names;
    }

    private static List<string> GenerateFantasyNames(string gender, int count)
    {
        var names = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var prefix = FantasyPrefixes[Random.Next(FantasyPrefixes.Length)];
            var suffix = FantasySuffixes[Random.Next(FantasySuffixes.Length)];
            var name = prefix + suffix;

            // 女性名倾向加 a 结尾
            if (gender == "female" && !name.EndsWith('a'))
            {
                name = name.TrimEnd('n', 's') + "a";
            }

            names.Add(name);
        }
        return names;
    }

    private static List<string> GenerateJapaneseNames(string gender, int count)
    {
        var names = new List<string>(count);
        var givenNames = gender == "female" ? JapaneseFemaleNames : JapaneseMaleNames;
        for (var i = 0; i < count; i++)
        {
            var surname = JapaneseSurnames[Random.Next(JapaneseSurnames.Length)];
            var given = givenNames[Random.Next(givenNames.Length)];
            names.Add($"{surname} {given}");
        }
        return names;
    }

    private LLMToolExecutionResult Failure(string errorCode, string message)
    {
        return new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = false,
            ErrorCode = errorCode,
            Content = message
        };
    }

    private sealed class NameArguments
    {
        /// <summary>
        /// 命名风格: chinese(默认), english, fantasy, japanese
        /// </summary>
        public string Style { get; set; }

        /// <summary>
        /// 性别: male(默认), female
        /// </summary>
        public string Gender { get; set; }

        /// <summary>
        /// 生成数量；默认 3。
        /// </summary>
        public int? Count { get; set; }
    }
}
