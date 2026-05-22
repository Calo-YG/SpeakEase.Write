using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    /// <summary>
    /// LLM 协议级错误信息：包含错误码、类型和详细消息。
    /// </summary>
    public class ErrorInfo
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("param")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Param { get; set; }

        [JsonPropertyName("code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Code { get; set; }
    }
}
