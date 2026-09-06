namespace SpeakEase.Write.Application.Abstractions.AI;

/// <summary>
/// Chat 到 Agent Runtime 的标准运行请求。保留在 Application 抽象层，避免入口直接依赖 Infrastructure 实现。
/// </summary>
public sealed class AgentRuntimeRequest
{
    public string RunId { get; init; } = string.Empty;
    public string ClientMessageId { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public string WorkId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string UserMessage { get; init; } = string.Empty;
    public string SkillName { get; init; } = string.Empty;
    public int MaxIterations { get; init; } = 10;
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
    public bool EnableAutoToolDispatch { get; init; } = true;
}
