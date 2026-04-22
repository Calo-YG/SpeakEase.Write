using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
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
