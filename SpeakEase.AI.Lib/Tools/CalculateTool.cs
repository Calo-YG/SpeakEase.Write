using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using System.Data;
using System.Text.Json;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 数学计算工具：安全地求值数学表达式（基于 DataTable.Compute，仅支持算术运算）
/// </summary>
public sealed class CalculateTool : IToolExecutor
{
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "calculate",
            Description = "计算数学表达式的结果，支持加减乘除、取模和括号运算，例如 (3+5)*2",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["expression"] = new()
                    {
                        Type = "string",
                        Description = "要计算的数学表达式，如 3+5*2、(10-3)/7"
                    }
                },
                Required = ["expression"]
            }
        }
    };

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        string expression = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("expression", out var prop))
                expression = prop.GetString();
        }
        catch { /* 忽略 */ }

        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "缺少 expression 参数",
                ErrorCode = "missing_parameter"
            });
        }

        try
        {
            // 安全白名单：仅允许数字、运算符、括号、小数点、空格
            var sanitized = expression.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(sanitized, @"^[\d+\-*/().%\s]+$"))
            {
                return Task.FromResult(new ToolResult
                {
                    Success = false,
                    Content = "表达式包含不允许的字符，仅支持数字和算术运算符（+-*/().%）",
                    ErrorCode = "invalid_character"
                });
            }

            var result = new DataTable().Compute(sanitized, null);

            return Task.FromResult(new ToolResult
            {
                Success = true,
                Content = JsonSerializer.Serialize(new { expression = sanitized, result = result.ToString() })
            });
        }
        catch (EvaluateException ex)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = $"表达式求值失败: {ex.Message}",
                ErrorCode = "evaluate_error"
            });
        }
        catch (SyntaxErrorException ex)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = $"表达式语法错误: {ex.Message}",
                ErrorCode = "syntax_error"
            });
        }
    }
}
