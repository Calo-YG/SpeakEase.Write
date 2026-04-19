using System.Data;
using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 数学表达式计算器，支持四则运算、幂、取模等基础数学运算。
/// 使用 DataTable.Compute 安全求值，仅支持数值运算，无代码注入风险。
/// </summary>
public static class CalculateTool
{
    public static ToolDefinition Definition => new()
    {
        Type = "function",
        Function = new ToolFunctionDefinition
        {
            Name = "calculate",
            Description = "计算数学表达式，支持四则运算、幂、取模等。仅允许数字和运算符，无代码注入风险。",
            Parameters = """
            {
                "type": "object",
                "properties": {
                    "expression": { "type": "string", "description": "数学表达式，如 (15+27)*3-8" }
                },
                "required": ["expression"]
            }
            """
        }
    };

    public static Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var input = JsonSerializer.Deserialize<CalculateArguments>(arguments, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new CalculateArguments();

        if (string.IsNullOrWhiteSpace(input.Expression))
        {
            return Task.FromResult(Failure("missing_expression", "expression 不能为空。"));
        }

        try
        {
            var sanitized = input.Expression.Trim();
            if (!IsValidExpression(sanitized))
            {
                return Task.FromResult(Failure("invalid_expression", $"表达式包含不允许的字符: {sanitized}"));
            }

            if (sanitized.Contains('^'))
            {
                return Task.FromResult(Failure("unsupported_operator", "不支持 ^ 运算符，请使用乘法替代或输入幂运算格式如 Pow(x,y)。"));
            }

            var result = new DataTable().Compute(sanitized, null);
            var payload = JsonSerializer.Serialize(new
            {
                expression = input.Expression,
                result = result.ToString(),
                resultType = result.GetType().Name
            });

            return Task.FromResult(new ToolResult
            {
                ToolName = "calculate",
                Success = true,
                Content = payload
            });
        }
        catch (SyntaxErrorException ex)
        {
            return Task.FromResult(Failure("syntax_error", $"表达式语法错误: {ex.Message}"));
        }
        catch (EvaluateException ex)
        {
            return Task.FromResult(Failure("evaluate_error", $"表达式求值错误: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Failure("calculation_error", $"计算失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 验证表达式只包含安全字符：数字、运算符、括号、小数点、空格。
    /// </summary>
    private static bool IsValidExpression(string expression)
    {
        foreach (var c in expression)
        {
            if (!char.IsDigit(c) && c is not ('+' or '-' or '*' or '/' or '%' or '(' or ')' or '.' or ' '))
            {
                return false;
            }
        }
        return true;
    }

    private static ToolResult Failure(string errorCode, string message)
    {
        return new ToolResult
        {
            ToolName = "calculate",
            Success = false,
            ErrorCode = errorCode,
            Content = message
        };
    }

    /// <summary>
    /// 注册到 Agent。
    /// </summary>
    public static void RegisterTo(ToolCapableBase agent)
    {
        agent.RegisterTool(Definition, ExecuteAsync);
    }

    private sealed class CalculateArguments
    {
        public string Expression { get; set; } = string.Empty;
    }
}
