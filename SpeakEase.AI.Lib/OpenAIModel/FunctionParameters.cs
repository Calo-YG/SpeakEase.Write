using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    public sealed class FunctionParameters
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        [JsonPropertyName("properties")]
        public Dictionary<string, ParameterSchema> Properties { get; set; }

        [JsonPropertyName("required")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Required { get; set; }

        [JsonPropertyName("additionalProperties")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool AdditionalProperties { get; set; }
    }

    public sealed class ParameterSchema
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; }

        [JsonPropertyName("enum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<object> Enum { get; set; }

        [JsonPropertyName("items")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ParameterSchema Items { get; set; }

        [JsonPropertyName("properties")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, ParameterSchema> Properties { get; set; }
    }
}
