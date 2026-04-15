namespace AINWZ.Infrastructure.LLM.Models;

/// <summary>
/// LLM 工具定义。
/// </summary>
public sealed class LLMToolDefinition
{
    public string Type { get; set; } = "function";

    public LLMToolFunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// 工具函数定义。
/// </summary>
public sealed class LLMToolFunctionDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; }

    public object Parameters { get; set; }
}
