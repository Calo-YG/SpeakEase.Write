namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// 技能定义，描述 Agent 可用的技能及其关联的工具。
    /// </summary>
    public sealed class SkillDefinition
    {
        /// <summary>
        /// 技能名称（唯一标识）。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 技能描述。
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 技能系统提示词，激活该技能时注入到对话中。
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// 该技能默认启用的工具名称列表。
        /// </summary>
        public List<string> DefaultToolNames { get; set; } = new();
    }
}
