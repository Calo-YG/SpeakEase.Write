using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    /// <summary>
    /// User 消息：用户输入，支持纯文本、ContentPart 数组（图文混合）、图片 URL 等。
    /// </summary>
    public class UserMessage : ChatMessage
    {
        [JsonIgnore]
        public override string Role => "user";

        [JsonPropertyName("content")]
        public object Content { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; set; }
    }

    public class ContentPart
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Text { get; set; }

        [JsonPropertyName("image_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImageUrlContent ImageUrl { get; set; }
    }

    public class ImageUrlContent
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("detail")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Detail { get; set; }
    }
}
