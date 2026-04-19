namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// LLM 返回的工具调用请求。
    /// </summary>
    public sealed class ToolCall
    {
        /// <summary>
        /// 工具调用 ID，用于关联工具执行结果。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 调用类型，默认 "function"。
        /// </summary>
        public string Type { get; set; } = "function";

        /// <summary>
        /// 被调用的函数信息。
        /// </summary>
        public ToolFunctionCall Function { get; set; } = new();
    }

    /// <summary>
    /// 工具函数调用信息。
    /// </summary>
    public sealed class ToolFunctionCall
    {
        /// <summary>
        /// 函数名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 函数参数（JSON 字符串）。
        /// </summary>
        public string Arguments { get; set; } = string.Empty;
    }
}
