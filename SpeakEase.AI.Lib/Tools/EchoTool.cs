using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using System.Text.Json;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 回显工具：将输入参数原样返回，用于验证工具调用链路
/// </summary>
public sealed class EchoTool : IToolExecutor
{
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "echo",
            Description = "回显输入的消息内容，用于验证工具调用链路是否正常",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["message"] = new()
                    {
                        Type = "string",
                        Description = "要回显的消息内容"
                    }
                },
                Required = ["message"]
            }
        }
    };

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        string message = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("message", out var prop))
                message = prop.GetString() ?? string.Empty;
        }
        catch
        {
            message = arguments;
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Content = message
        });
    }
}
