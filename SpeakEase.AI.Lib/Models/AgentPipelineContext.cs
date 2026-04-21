namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// Pipeline Filter 上下文，携带当前 Agent 执行状态
/// </summary>
public sealed class AgentPipelineContext
{
    public int CurrentIteration { get; set; }
    public int MaxIterations { get; set; }
    public List<ToolResult> ExecutedToolResults { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}
