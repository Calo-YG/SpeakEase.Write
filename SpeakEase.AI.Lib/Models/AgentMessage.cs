namespace SpeakEase.AI.Lib.Models
{
    /// <summary>
    /// Agent 对话消息。
    /// </summary>
    public sealed class AgentMessage
    {
        /// <summary>
        /// 默认构造函数（用于反序列化）。
        /// </summary>
        public AgentMessage() { }

        /// <summary>
        /// 构造指定角色和内容的消息。
        /// </summary>
        /// <param name="role">消息角色：user / assistant / system / tool。</param>
        /// <param name="content">消息内容。</param>
        public AgentMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }

        /// <summary>
        /// 构造指定角色、内容、名称和工具调用 ID 的消息（通常用于 tool 角色）。
        /// </summary>
        /// <param name="role">消息角色。</param>
        /// <param name="content">消息内容。</param>
        /// <param name="name">工具名称。</param>
        /// <param name="toolCallId">关联的工具调用 ID。</param>
        public AgentMessage(string role, string content, string name, string toolCallId)
        {
            Role = role;
            Content = content;
            Name = name;
            ToolCallId = toolCallId;
        }

        /// <summary>
        /// 消息角色：user / assistant / system / tool。
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 消息内容。
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 消息名称（用于 tool 角色标识工具名称）。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 工具调用 ID（用于 tool 角色关联工具调用）。
        /// </summary>
        public string ToolCallId { get; set; }

        /// <summary>
        /// assistant 消息携带的工具调用列表。
        /// </summary>
        public List<ToolCall> ToolCalls { get; set; }
    }
}
