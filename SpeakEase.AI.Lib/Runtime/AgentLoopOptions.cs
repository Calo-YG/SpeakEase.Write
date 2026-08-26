namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// AgentLoop 的运行预算和策略边界。
/// </summary>
public sealed class AgentLoopOptions
{
    public int MaxIterations { get; init; } = 10;
    public int MaxToolCalls { get; init; } = 30;
    public int MaxOutputTokens { get; init; } = 2048;
    public int ContextWindowTokens { get; init; } = 32_000;
    public TimeSpan RunTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan ToolJournalCompletionTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool AllowParallelReadOnlyTools { get; init; }
}
