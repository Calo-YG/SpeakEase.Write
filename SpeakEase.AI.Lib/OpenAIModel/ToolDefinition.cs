using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    /// <summary>
    /// 工具定义：描述一个 LLM 可调用的函数，包含名称、描述、参数 Schema。
    /// 注册到 LLM 的 tools 列表中，LLM 根据意图选择调用。
    /// </summary>
    public sealed class ToolDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public FunctionDefinition Function { get; set; }
    }

    /// <summary>
    /// 函数定义：工具函数的名称、描述、参数 Schema
    /// </summary>
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
}
