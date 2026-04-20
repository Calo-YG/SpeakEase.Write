using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
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
}
