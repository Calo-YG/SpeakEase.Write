using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    /// <summary>
    /// System 消息：设置 LLM 的行为和角色。
    /// </summary>
    public class SystemMessage : ChatMessage
    {
        [JsonIgnore]
        public override string Role => "system";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; set; }
    }
}
