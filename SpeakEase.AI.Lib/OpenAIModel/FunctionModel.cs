using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    #region Function Calling 模型

    public sealed class ToolDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")] 
        public FunctionDefinition Function { get; set; }
    }

    public sealed class FunctionDefinition
    {
        [JsonPropertyName("name")] 
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; }

        [JsonPropertyName("parameters")]
        public FunctionParameters Parameters { get; set; }

        [JsonPropertyName("strict")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] 
        public bool Strict { get; set; }
    }

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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
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
        public Dictionary<string, ParameterSchema>? Properties { get; set; }
    }

    public class ToolCall
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = "function";
        [JsonPropertyName("function")] public FunctionCallDetail Function { get; set; } = new();
    }

    public class FunctionCallDetail
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")] 
        public string Arguments { get; set; } = string.Empty;

        public T ParseArguments<T>() where T : class
        {
            return JsonSerializer.Deserialize<T>(Arguments, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        }
    }

    public static class ToolChoice
    {
        public static string Auto => "auto";
        public static string None => "none";
        public static string Required => "required";
        public static object Function(string name) => new { type = "function", function = new { name } };
    }

    #endregion
}
