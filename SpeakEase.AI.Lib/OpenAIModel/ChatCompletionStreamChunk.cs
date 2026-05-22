using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    /// <summary>
    /// OpenAI Chat Completion 流式 SSE chunk。
    /// 流式响应中每个 data: 行反序列化为此类型，通过 StreamToolCallHelper 合并 delta 片段。
    /// </summary>
    public sealed class ChatCompletionStreamChunk
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("object")]
        public string Object { get; set; } = "chat.completion.chunk";

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("system_fingerprint")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SystemFingerprint { get; set; }

        [JsonPropertyName("choices")]
        public List<StreamChoice> Choices { get; set; } = new();

        [JsonPropertyName("usage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public UsageInfo Usage { get; set; }

        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ErrorInfo Error { get; set; }
    }
}
