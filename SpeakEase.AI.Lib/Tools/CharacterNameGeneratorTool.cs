using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using System.Text.Json;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 角色名生成工具：支持中文名、英文名、日文名、奇幻名等多种风格
/// </summary>
public sealed class CharacterNameGeneratorTool : IToolExecutor
{
    // 中文姓氏（百家姓前108个）
    private static readonly string[] SurnamesCn = ["赵","钱","孙","李","周","吴","郑","王","冯","陈","褚","卫","蒋","沈","韩","杨","朱","秦","许","何","吕","张","曹","谢","邹","苏","潘","葛","范","彭","鲁","韦","马","苗","凤","花","方","俞","任","袁","柳","唐","罗","薛","雷","贺","龙","段","温","顾","孟","黄","穆","萧","尹","姚","邵","湛","汪","祁","毛","禹","狄","米","贝","明","臧","计","成","戴","宋","茅","庞","熊","纪","舒","屈","项","祝","董","梁","杜","阮","蓝","闵","席","季","麻","强","路","童","程","嵇","邢","裴","丁","石","崔","高","龙","万","叶","黎","白","容","向","易","廖","庾","耿","文","庄","晏","司","巩","聂","晁","勾","敖","融","冷","辛","阚","那","简","饶","空","曾","母","沙","乜","养","鞠","须","丰","巢","关","蒯","相","查","后","荆","红","游","竺","权","逯","盖","益","桓","公","濮","扈"];
    private static readonly string[] GivenNamesCnMale = ["伟","强","磊","军","勇","杰","涛","明","超","刚","平","辉","健","俊","波","国","斌","宏","志","宁","兴","良","海","山","仁","鑫","建","文","博","诚","天","翔","飞","鹏","宇","辰","浩","睿","泽","逸"];
    private static readonly string[] GivenNamesCnFemale = ["芳","娜","静","敏","婷","丽","莉","燕","艳","娟","霞","秀","玲","桂","萍","慧","琳","璐","欣","瑶","梦","薇","晴","雪","怡","颖","蕾","洁","茜","媛","诗","雨","萱","彤","菲","月","云","露","霜","冰"];
    private static readonly string[] NamesEnMale = ["James","John","Robert","Michael","William","David","Richard","Joseph","Thomas","Alexander","Daniel","Matthew","Henry","Sebastian","Oliver","Ethan","Lucas","Nathan","Adrian","Victor"];
    private static readonly string[] NamesEnFemale = ["Mary","Emma","Olivia","Sophia","Isabella","Charlotte","Amelia","Harper","Evelyn","Abigail","Emily","Elizabeth","Avery","Ella","Scarlett","Grace","Lily","Chloe","Victoria","Riley"];
    private static readonly string[] NamesEnFantasy = ["Aldric","Baelor","Caelum","Dorian","Erevan","Faelan","Gaelen","Hadrian","Ithil","Jorah","Kaelen","Lucian","Maeril","Nyssa","Orion","Pyralis","Quentin","Ravenna","Seraphina","Theron"];
    private static readonly string[] SurnamesJp = ["佐藤","鈴木","高橋","田中","渡辺","伊藤","山本","中村","小林","加藤","吉田","山田","佐々木","山口","松本","井上","木村","林","斎藤","清水"];
    private static readonly string[] GivenNamesJpMale = ["翔","大輝","蓮","樹","陽翔","湊","颯","悠真","大和","新"];
    private static readonly string[] GivenNamesJpFemale = ["結愛","陽菜","咲","葵","紬","凛","結衣","愛","芽依","莉子"];

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "character_name_generator",
            Description = "为小说角色生成随机姓名，支持中文、英文、日文、奇幻风格",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["style"] = new()
                    {
                        Type = "string",
                        Description = "姓名风格：cn_male(中文名男)、cn_female(中文名女)、en_male(英文名男)、en_female(英文名女)、fantasy(奇幻)、jp_male(日文名男)、jp_female(日文名女)",
                        Enum = ["cn_male", "cn_female", "en_male", "en_female", "fantasy", "jp_male", "jp_female"]
                    },
                    ["count"] = new()
                    {
                        Type = "integer",
                        Description = "生成数量，默认5"
                    }
                },
                Required = ["style"]
            }
        }
    };

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        string style = null;
        int count = 5;

        try
        {
            // 解析 JSON arguments 中的 style 和 count 参数
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("style", out var s))
                style = s.GetString();
            if (doc.RootElement.TryGetProperty("count", out var c))
                // 限制生成数量 1-20，防止异常输入
                count = Math.Max(1, Math.Min(c.GetInt32(), 20));
        }
        catch { /* 忽略 JSON 解析错误 */ }

        if (string.IsNullOrEmpty(style))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "缺少 style 参数",
                ErrorCode = "missing_parameter"
            });
        }

        var random = Random.Shared;
        var names = new List<string>();

        // 根据 style 参数选择不同的姓名组合策略
        for (int i = 0; i < count; i++)
        {
            names.Add(style switch
            {
                "cn_male" => $"{SurnamesCn[random.Next(SurnamesCn.Length)]}{GivenNamesCnMale[random.Next(GivenNamesCnMale.Length)]}",
                "cn_female" => $"{SurnamesCn[random.Next(SurnamesCn.Length)]}{GivenNamesCnFemale[random.Next(GivenNamesCnFemale.Length)]}",
                "en_male" => $"{NamesEnMale[random.Next(NamesEnMale.Length)]} {NamesEnMale[random.Next(NamesEnMale.Length)]}",
                "en_female" => $"{NamesEnFemale[random.Next(NamesEnFemale.Length)]} {NamesEnFemale[random.Next(NamesEnFemale.Length)]}",
                "fantasy" => NamesEnFantasy[random.Next(NamesEnFantasy.Length)],
                "jp_male" => $"{SurnamesJp[random.Next(SurnamesJp.Length)]} {GivenNamesJpMale[random.Next(GivenNamesJpMale.Length)]}",
                "jp_female" => $"{SurnamesJp[random.Next(SurnamesJp.Length)]} {GivenNamesJpFemale[random.Next(GivenNamesJpFemale.Length)]}",
                _ => $"unknown_style:{style}"
            });
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = JsonSerializer.Serialize(new { style, count, names })
        });
    }
}
