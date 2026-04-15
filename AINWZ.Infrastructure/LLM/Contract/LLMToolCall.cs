namespace AINWZ.Infrastructure.LLM.Contract;
/// <summary>
/// 模型返回的工具调用。
/// </summary>
public sealed class LLMToolCall
{
    public string Id { get; set; }

    public string Type { get; set; } = "function";

    public LLMToolFunctionCall Function { get; set; } = new();
}

/// <summary>
/// 工具函数调用信息。
/// </summary>
public sealed class LLMToolFunctionCall
{
    public string Name { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;
}
