namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// 工具选择策略。
/// </summary>
public sealed class LLMToolChoice
{
    public string Type { get; set; } = "auto";

    public LLMToolChoiceFunction Function { get; set; }
}

/// <summary>
/// 指定工具函数。
/// </summary>
public sealed class LLMToolChoiceFunction
{
    public string Name { get; set; } = string.Empty;
}
