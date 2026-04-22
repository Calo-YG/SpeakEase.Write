using System.Text.Json;
using System.Text.Json.Serialization;
using SpeakEase.AI.Lib.OpenAIModel;
using Xunit;

namespace AINWZ.Tests.LLM;

public class ChatMessageSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Fact]
    public void SystemMessage_Serializes_WithRoleAndContent()
    {
        ChatMessage msg = ChatMessage.System("hello");
        var json = JsonSerializer.Serialize(msg, JsonOptions);

        Assert.Contains("\"role\":\"system\"", json);
        Assert.Contains("\"content\":\"hello\"", json);
    }

    [Fact]
    public void UserMessage_Serializes_WithRoleAndContent()
    {
        ChatMessage msg = ChatMessage.User("test message");
        var json = JsonSerializer.Serialize(msg, JsonOptions);

        Assert.Contains("\"role\":\"user\"", json);
        Assert.Contains("\"content\":\"test message\"", json);
    }

    [Fact]
    public void AssistantMessage_WithToolCalls_Serializes_ContentAsEmptyString()
    {
        ChatMessage msg = new AssistantMessage
        {
            Content = string.Empty,
            ToolCalls = new List<ToolCall>
            {
                new() { Id = "call_123", Type = "function", Function = new FunctionCallDetail { Name = "test", Arguments = "{}" } }
            }
        };
        var json = JsonSerializer.Serialize(msg, JsonOptions);

        Assert.Contains("\"role\":\"assistant\"", json);
        Assert.Contains("\"content\":\"\"", json); // content 必须是空字符串，不能是 null
        Assert.Contains("\"tool_calls\"", json);
    }

    [Fact]
    public void ToolMessage_Serializes_WithRoleAndContent()
    {
        ChatMessage msg = ChatMessage.Tool("call_123", "result");
        var json = JsonSerializer.Serialize(msg, JsonOptions);

        Assert.Contains("\"role\":\"tool\"", json);
        Assert.Contains("\"content\":\"result\"", json);
        Assert.Contains("\"tool_call_id\":\"call_123\"", json);
    }

    [Fact]
    public void ChatMessageList_Serializes_AllTypes()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("system prompt"),
            ChatMessage.User("user message"),
            ChatMessage.Assistant("assistant reply"),
            ChatMessage.Tool("call_1", "tool result")
        };

        var json = JsonSerializer.Serialize(messages, JsonOptions);

        Assert.Contains("\"role\":\"system\"", json);
        Assert.Contains("\"role\":\"user\"", json);
        Assert.Contains("\"role\":\"assistant\"", json);
        Assert.Contains("\"role\":\"tool\"", json);
    }

    [Fact]
    public void ChatMessageList_ProducesValidJson()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.System("system prompt"),
            ChatMessage.User("user message")
        };

        var json = JsonSerializer.Serialize(messages, JsonOptions);

        // 验证是合法 JSON
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(2, root.GetArrayLength());

        // 验证 system message
        var sysMsg = root[0];
        Assert.Equal("system", sysMsg.GetProperty("role").GetString());
        Assert.Equal("system prompt", sysMsg.GetProperty("content").GetString());

        // 验证 user message
        var userMsg = root[1];
        Assert.Equal("user", userMsg.GetProperty("role").GetString());
        Assert.Equal("user message", userMsg.GetProperty("content").GetString());
    }
}
