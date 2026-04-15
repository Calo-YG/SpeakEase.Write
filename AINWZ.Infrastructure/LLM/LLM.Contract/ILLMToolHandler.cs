namespace AINWZ.Infrastructure.LLM.LLM.Contract;

/// <summary>
/// LLM 工具处理器。
/// </summary>
public interface ILLMToolHandler
{
    string Name { get; }

    Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default);
}
