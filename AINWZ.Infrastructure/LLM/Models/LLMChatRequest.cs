namespace AINWZ.Infrastructure.LLM.Models;

/// <summary>
/// LLM 对话请求对象。
/// </summary>
public sealed class LLMChatRequest
{
    public string Model { get; set; }

    public List<string> FallbackModels { get; set; } = new();

    public string SystemPrompt { get; set; }

    public List<LLMChatMessage> Messages { get; set; } = new();

    public decimal? Temperature { get; set; }

    public int? MaxTokens { get; set; }

    public bool UseJsonMode { get; set; }

    public List<LLMToolDefinition> Tools { get; set; } = new();

    public LLMToolChoice ToolChoice { get; set; }

    public bool EnableAutoToolDispatch { get; set; } = true;

    /// <summary>
    /// Agent Loop 最大迭代次数。
    /// 默认 20，即 LLM 可自动调用工具最多 20 轮。
    /// 设为 0 或 1 表示仅单轮（不自动执行工具）。
    /// </summary>
    public int MaxIterations { get; set; } = 20;

    public string SkillName { get; set; }

    public string SkillOverridePrompt { get; set; }
}
