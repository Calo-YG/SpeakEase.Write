using System.Text;
using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 文本分析工具，支持字数统计、词频统计、文本摘要提取。
/// 适用于小说写作场景中的文本审阅与统计需求。
/// </summary>
public sealed class TextAnalyzerTool:IToolExecutor
{
    public static ToolDefinition Definition => new()
    {
        Type = "function",
        Function = new ToolFunctionDefinition
        {
            Name = "text_analyzer",
            Description = "文本分析工具，支持字数统计(stats)、词频统计(frequency)、摘要提取(summary)三种模式。",
            Parameters = """
            {
                "type": "object",
                "properties": {
                    "text": { "type": "string", "description": "待分析的文本内容" },
                    "analysisType": { "type": "string", "description": "分析类型: stats(统计), frequency(词频), summary(摘要)", "enum": ["stats", "frequency", "summary"] },
                    "topN": { "type": "integer", "description": "词频模式返回的 Top N，默认20" },
                    "maxSummaryLength": { "type": "integer", "description": "摘要模式的最大长度，默认200" }
                },
                "required": ["text"]
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
        var input = JsonSerializer.Deserialize<TextAnalyzerArguments>(arguments, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new TextAnalyzerArguments();

        if (string.IsNullOrWhiteSpace(input.Text))
        {
            return Task.FromResult(Failure("missing_text", "text 不能为空。"));
        }

        var text = input.Text;
        var analysis = input.AnalysisType?.ToLowerInvariant() ?? "stats";

        object result = analysis switch
        {
            "stats" => AnalyzeStats(text),
            "frequency" => AnalyzeFrequency(text, input.TopN ?? 20),
            "summary" => AnalyzeSummary(text, input.MaxSummaryLength ?? 200),
            _ => Failure("unknown_analysis_type", $"不支持的分析类型: {analysis}，可选: stats, frequency, summary")
        };

        if (result is ToolResult errorResult)
        {
            return Task.FromResult(errorResult);
        }

        var payload = JsonSerializer.Serialize(result);
        return Task.FromResult(new ToolResult
        {
            ToolName = "text_analyzer",
            Success = true,
            Content = payload
        });
    }

    private static object AnalyzeStats(string text)
    {
        var chineseCharCount = 0;
        var englishWordCount = 0;
        var digitCount = 0;
        var punctuationCount = 0;
        var whitespaceCount = 0;

        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                if (c > 0x4E00 && c < 0x9FFF)
                {
                    chineseCharCount++;
                }
                else
                {
                    if (!inWord) englishWordCount++;
                    inWord = true;
                }
            }
            else
            {
                inWord = false;
                if (char.IsDigit(c)) digitCount++;
                else if (char.IsWhiteSpace(c)) whitespaceCount++;
                else if (char.IsPunctuation(c)) punctuationCount++;
            }
        }

        var lines = text.Split('\n', StringSplitOptions.None);
        var nonEmptyLines = lines.Count(l => !string.IsNullOrWhiteSpace(l));
        var paragraphs = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);
        var readingMinutes = (chineseCharCount / 400.0) + (englishWordCount / 200.0);

        return new
        {
            analysisType = "stats",
            totalCharacters = text.Length,
            chineseCharacters = chineseCharCount,
            englishWords = englishWordCount,
            digits = digitCount,
            punctuation = punctuationCount,
            whitespace = whitespaceCount,
            totalLines = lines.Length,
            nonEmptyLines,
            paragraphs = paragraphs.Length,
            estimatedReadingMinutes = Math.Round(readingMinutes, 1)
        };
    }

    private static object AnalyzeFrequency(string text, int topN)
    {
        topN = Math.Clamp(topN, 1, 100);

        var charFreq = new Dictionary<char, int>();
        foreach (var c in text)
        {
            if (c > 0x4E00 && c < 0x9FFF)
            {
                if (!charFreq.TryGetValue(c, out var count)) count = 0;
                charFreq[c] = count + 1;
            }
        }

        var topChars = charFreq
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select(kv => new { character = kv.Key.ToString(), count = kv.Value })
            .ToList();

        var wordFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var currentWord = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetter(c) && c <= 0x9FFF)
            {
                currentWord.Append(char.ToLowerInvariant(c));
            }
            else if (currentWord.Length > 0)
            {
                var word = currentWord.ToString();
                if (word.Length >= 2)
                {
                    if (!wordFreq.TryGetValue(word, out var count)) count = 0;
                    wordFreq[word] = count + 1;
                }
                currentWord.Clear();
            }
        }

        if (currentWord.Length >= 2)
        {
            var word = currentWord.ToString();
            if (!wordFreq.TryGetValue(word, out var count)) count = 0;
            wordFreq[word] = count + 1;
        }

        var topWords = wordFreq
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select(kv => new { word = kv.Key, count = kv.Value })
            .ToList();

        return new { analysisType = "frequency", topN, topChineseCharacters = topChars, topEnglishWords = topWords };
    }

    private static object AnalyzeSummary(string text, int maxSummaryLength)
    {
        maxSummaryLength = Math.Clamp(maxSummaryLength, 50, 2000);

        var paragraphs = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);
        var firstParagraph = paragraphs.Length > 0 ? paragraphs[0].Trim() : text.Trim();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.Length > 0 ? lines[0].Trim() : string.Empty;
        var preview = text.Length > maxSummaryLength ? text[..maxSummaryLength] + "..." : text;
        var lastParagraph = paragraphs.Length > 1 ? paragraphs[^1].Trim() : string.Empty;

        return new
        {
            analysisType = "summary",
            firstLine = Truncate(firstLine, 200),
            firstParagraph = Truncate(firstParagraph, maxSummaryLength),
            lastParagraph = Truncate(lastParagraph, maxSummaryLength),
            preview = Truncate(preview, maxSummaryLength),
            totalLength = text.Length
        };
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }

    private static ToolResult Failure(string errorCode, string message)
    {
        return new ToolResult
        {
            ToolName = "text_analyzer",
            Success = false,
            ErrorCode = errorCode,
            Content = message
        };
    }

    private sealed class TextAnalyzerArguments
    {
        public string Text { get; set; } = string.Empty;
        public string AnalysisType { get; set; }
        public int? TopN { get; set; }
        public int? MaxSummaryLength { get; set; }
    }
}
