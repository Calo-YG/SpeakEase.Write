namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// 工具执行结果。
    /// </summary>
    public sealed class ToolResult
    {
        /// <summary>
        /// 关联的工具调用 ID。
        /// </summary>
        public string ToolCallId { get; set; }

        /// <summary>
        /// 工具名称。
        /// </summary>
        public string ToolName { get; set; }

        /// <summary>
        /// 执行是否成功。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 执行结果内容。
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 错误码（执行失败时）。
        /// </summary>
        public string ErrorCode { get; set; }

        public static ToolResult Ok(string content)
        {
            return new ToolResult { Success = true, Content = content };
        }

        public static ToolResult Fail(string message, string errorCode = null)
        {
            return new ToolResult { Success = false, Content = message, ErrorCode = errorCode };
        }
    }
}
