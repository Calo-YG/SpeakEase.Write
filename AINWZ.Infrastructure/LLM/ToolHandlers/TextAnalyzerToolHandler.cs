using System.Text;
using System.Text.Json;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM.ToolHandlers;

/// <summary>
/// 文本分析工具，支持字数统计、行数统计、关键词频率统计、文本摘要提取。
/// 适用于小说写作场景中的文本审阅与统计需求。
/// </summary>
public sealed class TextAnalyzerToolHandler : ILLMToolHandler
{
    /// <inheritdoc />
    public string Name => "text_analyzer";

    /// <inheritdoc />
    public LLMToolDefinition ToolDefinition => new()
    {
        Type = "function",
        Function = new LLMToolFunctionDefinition
        {
            Name = Name,
            Description = "文本分析工具，支持字数统计(stats)、词频统计(frequency)、摘要提取(summary)三种模式。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string", description = "待分析的文本内容" },
                    analysisType = new { type = "string", description = "分析类型: stats(统计), frequency(词频), summary(摘要)", @enum = new[] { "stats", "frequency", "summary" } },
                    topN = new { type = "integer", description = "词频模式返回的 Top N，默认20" },
                    maxSummaryLength = new { type = "integer", description = "摘要模式的最大长度，默认200" }
                },
                required = new[] { "text" }
            }
        }
    };

    /// <inheritdoc />
    public Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
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
    /// 基础统计：总字符数、总行数、段落数、中文字符数、英文单词数、数字数。
    /// </summary>
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
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        // 估算阅读时间（中文 400字/分钟，英文 200词/分钟）
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

    /// <summary>
    /// 词频统计：统计中文单字/双字词和英文单词的频率。
    /// </summary>
    private static object AnalyzeFrequency(string text, int topN)
    {
        topN = Math.Clamp(topN, 1, 100);

        // 中文字符频率
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

        // 英文单词频率
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
                if (word.Length >= 2) // 忽略单字母
                {
                    if (!wordFreq.TryGetValue(word, out var count)) count = 0;
                    wordFreq[word] = count + 1;
                }
                currentWord.Clear();
            }
        }
        // 处理末尾
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

        return new
        {
            analysisType = "frequency",
            topN,
            topChineseCharacters = topChars,
            topEnglishWords = topWords
        };
    }

    /// <summary>
    /// 文本摘要：提取前N个字符作为预览，提取首行/首段作为摘要。
    /// </summary>
    private static object AnalyzeSummary(string text, int maxSummaryLength)
    {
        maxSummaryLength = Math.Clamp(maxSummaryLength, 50, 2000);

        // 首段作为摘要
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var firstParagraph = paragraphs.Length > 0 ? paragraphs[0].Trim() : text.Trim();

        // 首行作为标题
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.Length > 0 ? lines[0].Trim() : string.Empty;

        // 截断预览
        var preview = text.Length > maxSummaryLength
            ? text[..maxSummaryLength] + "..."
            : text;

        // 提取最后一段（用于了解结尾）
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

    private sealed class TextAnalyzerArguments
    {
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// 分析类型: stats(默认), frequency, summary
        /// </summary>
        public string AnalysisType { get; set; }

        /// <summary>
        /// 词频统计返回的 TopN；默认 20。
        /// </summary>
        public int? TopN { get; set; }

        /// <summary>
        /// 摘要最大长度；默认 200。
        /// </summary>
        public int? MaxSummaryLength { get; set; }
    }
}
