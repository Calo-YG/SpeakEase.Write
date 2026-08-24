namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// Agent 的软指导资料。运行预算、权限和工具顺序不属于 PromptProfile。
/// </summary>
public sealed class PromptProfile
{
    public string Identity { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public IReadOnlyList<string> QualityCriteria { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> StyleHints { get; init; } = Array.Empty<string>();
    public string OutputContract { get; init; } = string.Empty;
}

public sealed class PromptCompositionContext
{
    public string TaskObjective { get; init; } = string.Empty;
    public IReadOnlyList<string> UserConstraints { get; init; } = Array.Empty<string>();
    public string ContextSummary { get; init; } = string.Empty;
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}
