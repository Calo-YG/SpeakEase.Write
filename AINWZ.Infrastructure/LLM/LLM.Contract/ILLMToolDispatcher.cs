namespace AINWZ.Infrastructure.LLM.LLM.Contract;

/// <summary>
/// LLM 工具调用分发器。
/// </summary>
public interface ILLMToolDispatcher
{
    Task<IReadOnlyList<LLMToolExecutionResult>> DispatchAsync(IReadOnlyList<LLMToolCall> toolCalls, CancellationToken cancellationToken = default);
}
