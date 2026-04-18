using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Application.Contracts.AI.Dto;

/// <summary>
/// LLM 对话接口请求对象。
/// 模型标识与备用模型列表由 ICurrentLLMOptions 根据用户激活配置自动填充，
/// 客户端无需（也不应）指定 Model / FallbackModels。
/// </summary>
public sealed class LLMChatRequestDto
{
    /// <summary>
    /// 系统提示词。
    /// </summary>
    public string SystemPrompt { get; set; }

    /// <summary>
    /// 对话消息。
    /// </summary>
    public List<LLMChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// 温度参数。
    /// </summary>
    public decimal Temperature { get; set; }

    /// <summary>
    /// 最大输出 token。
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// 是否启用 JSON 模式。
    /// </summary>
    public bool UseJsonMode { get; set; }

    /// <summary>
    /// 可供模型调用的工具定义列表。
    /// </summary>
    public List<LLMToolDefinition> Tools { get; set; } = new();

    /// <summary>
    /// 工具选择策略。
    /// </summary>
    public LLMToolChoice ToolChoice { get; set; }

    /// <summary>
    /// 是否启用自动工具分发（Agent Loop）。
    /// </summary>
    public bool EnableAutoToolDispatch { get; set; } = true;

    /// <summary>
    /// Agent Loop 最大迭代次数。默认 20，设为 0 或 1 表示仅单轮。
    /// </summary>
    public int MaxIterations { get; set; } = 20;

    /// <summary>
    /// 指定要应用的内部技能名称。
    /// </summary>
    public string SkillName { get; set; }

    /// <summary>
    /// 对技能默认系统提示词的覆盖内容；为空则使用技能默认值。
    /// </summary>
    public string SkillOverridePrompt { get; set; }
}
