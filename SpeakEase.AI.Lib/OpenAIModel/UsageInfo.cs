using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CachedTokens { get; set; }
    }
}
