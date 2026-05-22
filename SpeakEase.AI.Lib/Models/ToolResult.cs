namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// 工具执行结果：包含成功/失败状态、输出内容、错误码，以及静态工厂方法。
    /// </summary>
    public sealed class ToolResult
    {
        /// <summary>
        /// 关联的 ToolCall Id，用于回填到对话历史
        /// </summary>
        public string ToolCallId { get; set; }

        /// <summary>
        /// 工具名称
        /// </summary>
        public string ToolName { get; set; }

        /// <summary>
        /// 执行是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 工具输出内容（JSON 字符串或纯文本）
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 内容类型（如 html、json、text），供前端渲染使用
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// 扩展数据字典，存储额外元数据
        /// </summary>
        public Dictionary<string, string> ExtraData { get; set; }

        /// <summary>
        /// 错误码（如 missing_parameter、execution_error）
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static ToolResult Ok(string content)
        {
            return new ToolResult { Success = true, Content = content };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static ToolResult Fail(string message, string errorCode = null)
        {
            return new ToolResult { Success = false, Content = message, ErrorCode = errorCode };
        }
    }
}
