namespace AINWZ.Infrastructure.LLM.Models;
/// <summary>
/// LLM 技能定义。
/// </summary>
public sealed class LLMSkillDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; }

    public string SystemPrompt { get; set; }

    public List<LLMToolDefinition> DefaultTools { get; set; } = new();
}
