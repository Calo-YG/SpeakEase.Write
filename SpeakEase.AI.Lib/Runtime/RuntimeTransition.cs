namespace SpeakEase.AI.Lib.Runtime;

public sealed class RuntimeTransition
{
    public RuntimeState From { get; init; }
    public RuntimeState To { get; init; }
    public string Reason { get; init; } = string.Empty;
}
