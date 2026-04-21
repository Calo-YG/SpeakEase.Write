namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// 技能定义，包含预设的系统提示词，可动态注入 Agent 上下文
/// </summary>
public sealed class SkillDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string SystemPrompt { get; set; }
}
