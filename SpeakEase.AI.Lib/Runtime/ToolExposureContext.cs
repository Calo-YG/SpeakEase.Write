namespace SpeakEase.AI.Lib.Runtime;

public sealed class ToolExposureContext
{
    public string AgentName { get; init; } = string.Empty;
    public string Phase { get; init; } = "generate";
    public IReadOnlyList<string> AllowedGroups { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GrantedScopes { get; init; } = Array.Empty<string>();
    public bool HasExplicitConsent { get; init; }
    public int MaxTools { get; init; } = 12;
}
