namespace SpeakEase.Write.Application.Abstractions.AI;

public sealed class AgentChatRuntimeRequest
{
    public string WorkId { get; init; } = string.Empty;
    public string UserMessage { get; init; } = string.Empty;
    public string ClientMessageId { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public string SkillName { get; init; } = string.Empty;
    public int MaxIterations { get; init; } = 10;
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
    public bool EnableAutoToolDispatch { get; init; } = true;
}
