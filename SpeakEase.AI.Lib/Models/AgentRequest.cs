namespace SpeakEase.AI.Lib.Models;

using SpeakEase.AI.Lib.OpenAIModel;

/// <summary>
/// Agent 执行请求：包含模型配置、提示词、对话历史、生成参数等。
/// </summary>
public sealed class AgentRequest
{
    /// <summary>
    /// 指定使用的 LLM 模型名称，为空时使用默认模型
    /// </summary>
    public string Model { get; set; }
    /// <summary>
    /// 系统提示词，为空时使用 ReActAgent 内置默认提示词
    /// </summary>
    public string SystemPrompt { get; set; }
    /// <summary>
    /// 当前用户消息内容
    /// </summary>
    public string UserMessage { get; set; }
    /// <summary>
    /// 多轮对话历史，支持上下文连续对话
    /// </summary>
    public List<ChatMessage> ConversationHistory { get; set; } = new();
    /// <summary>
    /// 采样温度 (0-2)，值越大输出越随机
    /// </summary>
    public double? Temperature { get; set; }
    /// <summary>
    /// 单次响应最大 Token 数
    /// </summary>
    public int? MaxTokens { get; set; }
    public double? TopP { get; set; }
    public double? FrequencyPenalty { get; set; }
    public double? PresencePenalty { get; set; }
    /// <summary>
    /// ReAct 循环最大迭代次数，默认 10，防止无限循环
    /// </summary>
    public int MaxIterations { get; set; } = 10;
    /// <summary>
    /// 指定加载的技能名称
    /// </summary>
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
