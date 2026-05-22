using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    // 使用 System.Text.Json 多态序列化，按 "role" 字段区分具体消息类型
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "role")]
    [JsonDerivedType(typeof(SystemMessage), typeDiscriminator: "system")]
    [JsonDerivedType(typeof(UserMessage), typeDiscriminator: "user")]
    [JsonDerivedType(typeof(AssistantMessage), typeDiscriminator: "assistant")]
    [JsonDerivedType(typeof(ToolMessage), typeDiscriminator: "tool")]
    /// <summary>
    /// ChatMessage 抽象基类：定义消息角色并提工厂方法创建具体消息实例。
    /// 通过 JSON 多态序列化，不同 role 自动反序列化为对应的子类型。
    /// </summary>
    public abstract class ChatMessage
    {
        /// <summary>
        /// 消息角色标识（system / user / assistant / tool）
        /// </summary>
        [JsonIgnore]
        public abstract string Role { get; }

        public static SystemMessage System(string content) => new() { Content = content };
        public static UserMessage User(string content) => new() { Content = content };
        public static UserMessage User(List<ContentPart> content) => new() { Content = content };
        public static AssistantMessage Assistant(string content) => new() { Content = content };
        public static AssistantMessage Assistant(string content, string reasoningContent) => new() { Content = content, ReasoningContent = reasoningContent };
        public static AssistantMessage Assistant(List<ToolCall> toolCalls) => new() { ToolCalls = toolCalls, Content = string.Empty };
        public static ToolMessage Tool(string toolCallId, string content) => new() { ToolCallId = toolCallId, Content = content };
    }
}
