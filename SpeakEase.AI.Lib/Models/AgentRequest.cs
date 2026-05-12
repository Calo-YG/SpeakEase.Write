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
    public double? TopP { get; set; }
    public double? FrequencyPenalty { get; set; }
    public double? PresencePenalty { get; set; }
    public int MaxIterations { get; set; } = 10;
    public string SkillName { get; set; }

    /// <summary>
    /// 关联的作品标识，供 Agent 内部工具链使用
    /// </summary>
    public string WorkId { get; set; }

    /// <summary>
    /// 发起请求的用户标识
    /// </summary>
    public string UserId { get; set; }
}
