namespace AINWZ.Infrastructure.LLM;
/// <summary>
/// LLM 对话消息。
/// </summary>
public sealed record LLMChatMessage(
    string Role,
    string Content,
    string Name = null,
    string ToolCallId = null,
    List<LLMToolCall> ToolCalls = null);
