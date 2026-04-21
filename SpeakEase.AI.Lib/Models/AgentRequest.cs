namespace SpeakEase.AI.Lib.Models;

using SpeakEase.AI.Lib.OpenAIModel;

/// <summary>
/// Agent 执行请求
/// </summary>
public sealed class AgentRequest
{
    public string Model { get; set; }
    public string SystemPrompt { get; set; }
    public string UserMessage { get; set; }
    public List<ChatMessage> ConversationHistory { get; set; } = new();
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public int MaxIterations { get; set; } = 10;
    public string SkillName { get; set; }
}
