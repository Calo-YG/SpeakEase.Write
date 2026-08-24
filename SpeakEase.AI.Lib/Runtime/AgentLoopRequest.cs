using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Runtime;

/// <summary>
/// AgentLoop 的一次执行请求。运行时只依赖 AI 协议和能力接口，不依赖业务层或持久化层。
/// </summary>
public sealed class AgentLoopRequest
{
    public string RunId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public AgentRequest Request { get; init; } = new();
    public IChatCompatible Llm { get; init; }
    public IToolCapable Tools { get; init; }
    public AgentLoopOptions Options { get; init; } = new();
    public ISkillResolver SkillResolver { get; init; }
}
