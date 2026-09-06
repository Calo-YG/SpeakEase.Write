namespace SpeakEase.AI.Lib.Runtime;

public sealed class PromptCompileRequest
{
    public string ProfileKey { get; init; } = string.Empty;
    public string TaskObjective { get; init; } = string.Empty;
    public IReadOnlyList<string> UserConstraints { get; init; } = Array.Empty<string>();
    public string ContextSummary { get; init; } = string.Empty;
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
    public string OutputContract { get; init; } = string.Empty;
    public PromptProfile FallbackProfile { get; init; }
}
