namespace SpeakEase.Write.Infrastructure.AI.Runtime;

public sealed class AgentRuntimeModeOptions
{
    public const string SectionName = "AiRuntime";

    public string Mode { get; set; } = "legacy";
    public bool EnableDynamicToolExposure { get; set; }
    public bool EnableCharacterSelfGrowth { get; set; }
}
