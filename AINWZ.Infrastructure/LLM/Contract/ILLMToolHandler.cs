using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM.Contract;

/// <summary>
/// LLM 工具处理器。
/// </summary>
public interface ILLMToolHandler
{
    string Name { get; }

    /// <summary>
    /// 工具的完整定义（含 parameters JSON Schema），供 LLM 识别调用参数。
    /// </summary>
    LLMToolDefinition ToolDefinition { get; }

    Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default);
}
