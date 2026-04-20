using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    #region 消息模型

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "role")]
    [JsonDerivedType(typeof(SystemMessage), typeDiscriminator: "system")]
    [JsonDerivedType(typeof(UserMessage), typeDiscriminator: "user")]
    [JsonDerivedType(typeof(AssistantMessage), typeDiscriminator: "assistant")]
    [JsonDerivedType(typeof(ToolMessage), typeDiscriminator: "tool")]
    public abstract class ChatMessage
    {
        [JsonPropertyName("role")]
        public abstract string Role { get; }

        public static SystemMessage System(string content) => new() { Content = content };
        public static UserMessage User(string content) => new() { Content = content };
        public static UserMessage User(List<ContentPart> content) => new() { Content = content };
        public static AssistantMessage Assistant(string content) => new() { Content = content };
        public static AssistantMessage Assistant(List<ToolCall> toolCalls) => new() { ToolCalls = toolCalls, Content = null };
        public static ToolMessage Tool(string toolCallId, string content) => new() { ToolCallId = toolCallId, Content = content };
    }

    public class SystemMessage : ChatMessage
    {
        public override string Role => "system";

        [JsonPropertyName("content")] 
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("name")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Name { get; set; }
    }

    public class UserMessage : ChatMessage
    {
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

        //public static ContentPart Text(string text) => new() { Type = "text", Text = text };
        //public static ContentPart ImageUrl(string url, string? detail = null) => new() { Type = "image_url", ImageUrl = new ImageUrlContent { Url = url, Detail = detail } };
    }

    public class ImageUrlContent
    {
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;

        [JsonPropertyName("detail")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Detail { get; set; }
    }

    public class AssistantMessage : ChatMessage
    {
        public override string Role => "assistant";
        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Content { get; set; }
        [JsonPropertyName("refusal")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Refusal { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public List<ToolCall> ToolCalls { get; set; }

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string Name { get; set; }
    }

    public class ToolMessage : ChatMessage
    {
        public override string Role => "tool";
        [JsonPropertyName("tool_call_id")]
        public string ToolCallId { get; set; } = string.Empty;

        [JsonPropertyName("content")] 
        public string Content { get; set; } = string.Empty;
    }

    #endregion
}
