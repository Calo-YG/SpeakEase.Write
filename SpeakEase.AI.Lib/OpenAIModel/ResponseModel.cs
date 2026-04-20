using SpeakEase.AI.Lib.OpenAIModels;
using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    #region 响应模型

    public class ChatCompletionResponse
    {
        [JsonPropertyName("id")] 
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("object")] 
        public string Object { get; set; } = "chat.completion";

        [JsonPropertyName("created")] 
        public long Created { get; set; }

        [JsonPropertyName("model")] 
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("system_fingerprint")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string SystemFingerprint { get; set; }

        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; }

        [JsonPropertyName("usage")]
        public UsageInfo Usage { get; set; }

        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public ErrorInfo Error { get; set; }

        [JsonIgnore]
        public bool HasToolCalls => Choices.Count > 0 && (Choices[0].Message?.ToolCalls?.Any() ?? false);
        [JsonIgnore]
        public ToolCall FirstToolCall => Choices.Count > 0 ? Choices[0].Message?.ToolCalls?.FirstOrDefault() : null;
    }

    public class Choice
    {
        [JsonPropertyName("index")] 
        public int Index { get; set; }

        [JsonPropertyName("message")] 
        public AssistantMessage? Message { get; set; }

        [JsonPropertyName("delta")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DeltaMessage? Delta { get; set; }

        [JsonPropertyName("logprobs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public LogprobsInfo? Logprobs { get; set; }

        [JsonPropertyName("finish_reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FinishReason { get; set; }
    }

    public class DeltaMessage
    {
        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Role { get; set; }

        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Content { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ToolCallDelta> ToolCalls { get; set; }

        [JsonPropertyName("refusal")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Refusal { get; set; }
    }

    public class ToolCallDelta
    {
        [JsonPropertyName("index")] 
        public int Index { get; set; }

        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Id { get; set; }

        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Type { get; set; }

        [JsonPropertyName("function")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FunctionCallDelta Function { get; set; }
    }

    public class FunctionCallDelta
    {
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Name { get; set; }

        [JsonPropertyName("arguments")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Arguments { get; set; }
    }

    public class UsageInfo
    {
        [JsonPropertyName("prompt_tokens")] 
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")] 
        public int TotalTokens { get; set; }

        [JsonPropertyName("prompt_tokens_details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public PromptTokensDetails PromptTokensDetails { get; set; }
    }

    public class PromptTokensDetails
    {
        [JsonPropertyName("cached_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public int CachedTokens { get; set; }
    }

    public class LogprobsInfo
    {
        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<TokenLogprob> Content { get; set; }
    }

    public class TokenLogprob
    {
        [JsonPropertyName("token")] 
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("logprob")] 
        public double Logprob { get; set; }

        [JsonPropertyName("bytes")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public List<int> Bytes { get; set; }
    }

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

    #endregion
}
