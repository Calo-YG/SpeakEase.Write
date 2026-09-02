using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Runtime;

public sealed class ToolCapabilityDescriptor
{
    public string Name { get; init; } = string.Empty;
    public string Group { get; init; } = "system.legacy";
    public string RiskLevel { get; init; } = "medium";
    public bool ReadOnly { get; init; }
    public bool RequiresExplicitConsent { get; init; }
    public IReadOnlyList<string> RequiredScopes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredPhases { get; init; } = Array.Empty<string>();
    public ToolDefinition Definition { get; init; }
}
