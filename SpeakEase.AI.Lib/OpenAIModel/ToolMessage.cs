using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    /// <summary>
    /// Tool 消息：工具执行结果。
    /// 将工具返回值回填到对话历史，关联对应的 tool_call_id，LLM 在下一轮可据此继续推理。
    /// </summary>
    public class ToolMessage : ChatMessage
    {
        [JsonIgnore]
        public override string Role => "tool";

        [JsonPropertyName("tool_call_id")]
        public string ToolCallId { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
