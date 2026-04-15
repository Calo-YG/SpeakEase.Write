using AINWZ.Infrastructure.LLM.LLM.Contract;

namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// 默认 LLM 工具调用分发器。
/// </summary>
public sealed class LLMToolDispatcher : ILLMToolDispatcher
{
    private readonly IReadOnlyDictionary<string, ILLMToolHandler> _toolHandlers;

    /// <summary>
    /// 初始化工具调用分发器。
    /// </summary>
    /// <param name="toolHandlers">已注册的工具处理器。</param>
    public LLMToolDispatcher(IEnumerable<ILLMToolHandler> toolHandlers)
    {
        _toolHandlers = toolHandlers.ToDictionary(handler => handler.Name, StringComparer.OrdinalIgnoreCase);
    }

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
}
