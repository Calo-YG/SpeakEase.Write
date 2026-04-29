namespace SpeakEase.Write.Application.Contracts.AI.Dto;

/// <summary>
/// Agent 对话请求 DTO
/// </summary>
public sealed class AgentChatRequestDto
{
    /// <summary>
    /// 所属作品标识（创作 Agent 必填）
    /// </summary>
    public string WorkId { get; set; }

    /// <summary>
    /// 对话消息列表（前端格式：role + content）
    /// </summary>
    public List<AgentChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// 技能名称（可选）
    /// </summary>
    public string SkillName { get; set; }

    /// <summary>
    /// 温度（可选，默认 0.7）
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// 最大 Token 数（可选）
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// 最大迭代次数（默认 10）
    /// </summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// 是否启用自动工具调度
    /// </summary>
    public bool EnableAutoToolDispatch { get; set; } = true;
}

/// <summary>
/// 前端传入的对话消息格式
/// </summary>
public sealed class AgentChatMessage
{
    public string Role { get; set; }
    public string Content { get; set; }
}
