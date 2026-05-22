namespace SpeakEase.Write.Infrastructure.AI.Contract;

// Agent元数据配置记录：描述Agent的内容类型、记忆策略、默认LLM参数
public sealed record AgentMetadata
{
    public string ContentType { get; init; } = "plain"; // 内容类型，如chapter/outline/character/setting等
    public bool NeedsProjectMemory { get; init; } = true; // 是否需要注入项目上下文记忆
    public bool ShouldFilterHistory { get; init; } // 是否需要过滤对话历史（如写作Agent需要清空旧内容）
    public AgentParameters DefaultParameters { get; init; } = AgentParameters.Default; // 默认LLM调用参数
}

// Agent LLM调用参数记录：温度、TopP采样、频率惩罚、存在惩罚、最大输出Token数
public sealed record AgentParameters(
    double Temperature,
    double? TopP = null,
    double? FrequencyPenalty = null,
    double? PresencePenalty = null,
    int MaxTokens = 2048)
{
    public static AgentParameters Default => new(0.7, MaxTokens: 2048); // 全局默认参数
}
