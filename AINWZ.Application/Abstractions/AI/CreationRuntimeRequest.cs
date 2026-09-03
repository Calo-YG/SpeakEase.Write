namespace SpeakEase.Write.Application.Abstractions.AI;

public sealed class CreationRuntimeRequest
{
    public AgentRuntimeRequest Request { get; init; } = new();
    public string RuntimeMode { get; init; } = string.Empty;
    public bool? EnableDynamicToolExposure { get; init; }
}
