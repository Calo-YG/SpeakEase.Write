namespace AINWZ.Infrastructure.LLM;

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

    public string SkillName { get; set; }

    public string SkillOverridePrompt { get; set; }
}
