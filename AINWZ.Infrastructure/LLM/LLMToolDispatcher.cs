using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// 默认 LLM 工具调用分发器。
/// </summary>
/// <remarks>
/// 初始化工具调用分发器。
/// </remarks>
/// <param name="toolHandlers">已注册的工具处理器。</param>
public sealed class LLMToolDispatcher(IEnumerable<ILLMToolHandler> toolHandlers) : ILLMToolDispatcher
{
    private readonly IReadOnlyDictionary<string, ILLMToolHandler> _toolHandlers = toolHandlers.ToDictionary(handler => handler.Name, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LLMToolExecutionResult>> DispatchAsync(IReadOnlyList<LLMToolCall> toolCalls, CancellationToken cancellationToken = default)
    {
        var results = new List<LLMToolExecutionResult>(toolCalls.Count);

        foreach (var toolCall in toolCalls)
        {
            if (!_toolHandlers.TryGetValue(toolCall.Function.Name, out var handler))
            {
                results.Add(new LLMToolExecutionResult
                {
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Function.Name,
                    Success = false,
                    ErrorCode = "tool_not_found",
                    Content = $"工具未注册: {toolCall.Function.Name}"
                });
                continue;
            }

            try
            {
                var result = await handler.ExecuteAsync(toolCall.Function.Arguments, cancellationToken);
                result.ToolCallId ??= toolCall.Id;
                result.ToolName = string.IsNullOrWhiteSpace(result.ToolName) ? toolCall.Function.Name : result.ToolName;
                results.Add(result);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(new LLMToolExecutionResult
                {
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Function.Name,
                    Success = false,
                    ErrorCode = "tool_execution_failed",
                    Content = exception.Message
                });
            }
        }

        return results;
    }

    /// <summary>
    /// 获取指定名称的工具完整定义（含 parameters JSON Schema）。
    /// </summary>
    public LLMToolDefinition GetToolDefinition(string toolName)
    {
        return _toolHandlers.TryGetValue(toolName, out var handler) ? handler.ToolDefinition : null;
    }

    /// <summary>
    /// 获取所有已注册工具的完整定义。
    /// </summary>
    public IReadOnlyList<LLMToolDefinition> GetAllToolDefinitions()
    {
        return _toolHandlers.Values.Select(h => h.ToolDefinition).Where(d => d is not null).ToList();
    }
}
