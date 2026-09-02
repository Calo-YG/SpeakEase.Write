namespace SpeakEase.AI.Lib.Runtime;

public sealed class AgentDescriptor
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public IReadOnlyList<string> InputKinds { get; init; } = Array.Empty<string>();
    public string OutputKind { get; init; } = string.Empty;
    public string PromptProfileKey { get; init; } = string.Empty;
    public string PolicyProfileKey { get; init; } = string.Empty;
    public IReadOnlyList<string> ToolGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MemoryScopes { get; init; } = Array.Empty<string>();
}
