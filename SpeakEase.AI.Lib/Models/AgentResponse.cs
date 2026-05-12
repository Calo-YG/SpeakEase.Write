namespace SpeakEase.AI.Lib.Models;

using SpeakEase.AI.Lib.OpenAIModel;

/// <summary>
/// Agent 执行响应
/// </summary>
public sealed class AgentResponse
{
    public string Content { get; set; }
    public string ReasoningContent { get; set; }
    public string Model { get; set; }
    public List<ToolResult> ToolResults { get; set; } = new();
    public List<ChatMessage> ConversationHistory { get; set; } = new();
    public int Iterations { get; set; }
    public string StopReason { get; set; }
    public UsageInfo TotalUsage { get; set; }
}
