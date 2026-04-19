using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 回显工具，用于验证工具调用闭环。
/// </summary>
public static class EchoTool
{
    public static ToolDefinition Definition => new()
    {
        Type = "function",
        Function = new ToolFunctionDefinition
        {
            Name = "echo",
            Description = "回显输入内容，用于验证工具调用闭环。",
            Parameters = """
            {
                "type": "object",
                "properties": {
                    "message": { "type": "string", "description": "要回显的消息内容" }
                },
                "required": ["message"]
            }
            """
        }
    };

    public static Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolResult
        {
            ToolName = "echo",
            Success = true,
            Content = arguments
        });
    }

    /// <summary>
    /// 注册到 Agent。
    /// </summary>
    public static void RegisterTo(ToolCapableBase agent)
    {
        agent.RegisterTool(Definition, ExecuteAsync);
    }
}
