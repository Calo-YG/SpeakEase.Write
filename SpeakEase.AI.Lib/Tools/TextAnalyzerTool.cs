using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using System.Text.Json;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 文本分析工具：统计字数、词数、句数、段落数，提取摘要（截取前N字）
/// </summary>
public sealed class TextAnalyzerTool : IToolExecutor
{
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "text_analyzer",
            Description = "分析文本内容，统计字数、词数、句数、段落数，并可选提取摘要",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["text"] = new()
                    {
                        Type = "string",
                        Description = "要分析的文本内容"
                    },
                    ["summary_length"] = new()
                    {
                        Type = "integer",
                        Description = "摘要截取长度（字符数），0 表示不生成摘要，默认100"
                    }
                },
                Required = ["text"]
            }
        }
    };

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        string text = null;
        int summaryLength = 100;

        try
        {
            // 从 JSON arguments 中提取 text 和 summary_length 参数
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("text", out var textProp))
                text = textProp.GetString();
            if (root.TryGetProperty("summary_length", out var lenProp))
                summaryLength = lenProp.GetInt32();
        }
        catch { /* 忽略 JSON 解析错误 */ }

        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "缺少 text 参数",
                ErrorCode = "missing_parameter"
            });
        }

        // 统计各项指标
        var charCount = text.Length;
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        // 使用正则匹配中英文句子结束符号
        var sentenceCount = System.Text.RegularExpressions.Regex.Matches(text, @"[。！？.!?\n]").Count;
        if (sentenceCount == 0) sentenceCount = 1; // 至少算 1 句
        var paragraphCount = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries).Length;

        // 按 summary_length 截取摘要
        string summary = summaryLength > 0 && text.Length > summaryLength
            ? text[..summaryLength] + "..."
            : summaryLength > 0 ? text : null;

        var result = JsonSerializer.Serialize(new
        {
            char_count = charCount,
            word_count = wordCount,
            sentence_count = sentenceCount,
            paragraph_count = paragraphCount,
            summary
        });

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = result
        });
    }
}
