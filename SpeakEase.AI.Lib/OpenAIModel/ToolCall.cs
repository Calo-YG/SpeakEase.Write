using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    /// <summary>
    /// 工具调用定义：LLM 请求执行的函数调用，包含 id、type 和 function 详情。
    /// </summary>
    public class ToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public FunctionCallDetail Function { get; set; } = new();
    }

    /// <summary>
    /// 函数调用详情：函数名和 JSON 格式的参数字符串。
    /// </summary>
    public class FunctionCallDetail
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// 将 Arguments JSON 字符串反序列化为指定类型 T
        /// </summary>
        public T ParseArguments<T>() where T : class
        {
            return JsonSerializer.Deserialize<T>(Arguments, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        }
    }
}
