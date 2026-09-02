namespace SpeakEase.AI.Lib.Runtime;

public enum RuntimeState
{
    Created,
    Running,
    WaitingTool,
    WaitingInterrupt,
    Paused,
    Completed,
    Failed,
    Cancelled,
    TimedOut,
    MaxIterationsReached
}
