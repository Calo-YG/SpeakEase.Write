using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// 回显工具，用于验证工具调用闭环。
/// </summary>
public  class EchoTool:IToolExecutor
{
    /// <summary>
    /// 公共获取工具定义属性，供 Agent 注册时使用。
    /// </summary>
    public ToolDefinition ToolDefinition => Definition;

    private static ToolDefinition Definition => new()
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

    /// <summary>
    /// 工具执行方法，接收输入参数并返回结果。此处直接将输入参数作为回显内容返回。
    /// </summary>
    /// <param name="arguments"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolResult
        {
            ToolName = "echo",
            Success = true,
            Content = arguments
        });
    }
}
