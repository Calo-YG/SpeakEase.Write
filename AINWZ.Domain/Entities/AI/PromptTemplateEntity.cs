namespace AINWZ.Domain.Entities.AI
{
    /// <summary>
    /// Prompt 模板实体，用于管理续写、润色、角色设定等提示模板。
    /// </summary>
    public class PromptTemplateEntity : Entity
    {
        /// <summary>
        /// 模板名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 适用场景。
        /// </summary>
        public string Scenario { get; set; } = string.Empty;

        /// <summary>
        /// 模板内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 模板版本。
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 是否为系统模板。
        /// </summary>
        public bool IsSystemTemplate { get; set; }

        /// <summary>
        /// 模板变量定义。
        /// </summary>
        public Dictionary<string, string> Variables { get; set; } = new();
    }
}
