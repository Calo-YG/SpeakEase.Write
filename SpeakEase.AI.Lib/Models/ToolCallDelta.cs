namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// 流式工具调用增量，用于逐步拼接完整的工具调用。
    /// </summary>
    public sealed class ToolCallDelta
    {
        /// <summary>
        /// 工具调用索引。
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 工具调用 ID 增量。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 调用类型增量。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 函数名称增量。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 函数参数增量。
        /// </summary>
        public string Arguments { get; set; }
    }
}
