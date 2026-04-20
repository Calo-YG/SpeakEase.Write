using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    #region 流式专用模型
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

    /// <summary>
    /// 
    /// </summary>
    public sealed class StreamChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("delta")] 
        public StreamDelta Delta { get; set; }

        [JsonPropertyName("logprobs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public LogprobsInfo Logprobs { get; set; }

        [JsonPropertyName("finish_reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string FinishReason { get; set; }
    }

    public sealed class StreamDelta
    {
        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Role { get; set; }

        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Content { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public List<StreamToolCallDelta> ToolCalls { get; set; }

        [JsonPropertyName("refusal")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Refusal { get; set; }
    }

    public sealed class StreamToolCallDelta
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public string Id { get; set; }
        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string? Type { get; set; }

        [JsonPropertyName("function")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public StreamFunctionDelta Function { get; set; }
    }

    public sealed class StreamFunctionDelta
    {
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; set; }

        [JsonPropertyName("arguments")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Arguments { get; set; }
    }

    #endregion
}
