using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
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
