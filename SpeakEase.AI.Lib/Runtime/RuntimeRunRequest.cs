namespace SpeakEase.AI.Lib.Runtime;

public sealed class RuntimeRunRequest
{
    public AgentLoopRequest LoopRequest { get; init; } = new();
    public AgentRuntimeOptions Options { get; init; } = new();
    public bool PublishEvents { get; init; } = true;
}
