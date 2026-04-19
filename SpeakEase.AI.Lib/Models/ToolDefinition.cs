namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// 工具定义，描述 Agent 可用的工具及其参数规范。
    /// </summary>
    public sealed class ToolDefinition
    {
        /// <summary>
        /// 工具类型，默认 "function"。
        /// </summary>
        public string Type { get; set; } = "function";

        /// <summary>
        /// 工具函数定义。
        /// </summary>
        public ToolFunctionDefinition Function { get; set; }
    }

    /// <summary>
    /// 工具函数定义。
    /// </summary>
    public sealed class ToolFunctionDefinition
    {
        /// <summary>
        /// 函数名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 函数描述，供 LLM 理解用途。
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 参数 JSON Schema（字符串形式），描述函数参数结构。
        /// </summary>
        public string Parameters { get; set; }
    }
}
