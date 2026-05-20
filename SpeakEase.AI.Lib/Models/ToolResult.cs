namespace SpeakEase.AI.Lib.Models
{
    public sealed class ToolResult
    {
        public string ToolCallId { get; set; }

        public string ToolName { get; set; }

        public bool Success { get; set; }

        public string Content { get; set; }

        public string ContentType { get; set; }

        public Dictionary<string, string> ExtraData { get; set; }

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
