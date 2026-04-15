using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM.ToolHandlers;

/// <summary>
/// 最小内置工具处理器，用于验证自动工具分发闭环。
/// </summary>
public sealed class EchoToolHandler : ILLMToolHandler
{
    /// <inheritdoc />
    public string Name => "echo";

    /// <inheritdoc />
    public Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            Content = arguments
        });
    }
}
