namespace SpeakEase.Write.Infrastructure.AI.Contract;

public sealed record AgentMetadata
{
    public string ContentType { get; init; } = "plain";
    public bool NeedsProjectMemory { get; init; } = true;
    public bool ShouldFilterHistory { get; init; }
    public AgentParameters DefaultParameters { get; init; } = AgentParameters.Default;
}

public sealed record AgentParameters(
    double Temperature,
    double? TopP = null,
    double? FrequencyPenalty = null,
    double? PresencePenalty = null,
    int MaxTokens = 2048)
{
    public static AgentParameters Default => new(0.7, MaxTokens: 2048);
}
