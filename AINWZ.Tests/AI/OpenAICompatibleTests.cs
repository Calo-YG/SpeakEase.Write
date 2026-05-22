using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace AINWZ.Tests.AI;

public sealed class OpenAICompatibleTests
{
    [Fact]
    public async Task ChatAsync_ClampsRequestedMaxTokensToConfiguredLimit()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"id\":\"req-1\",\"model\":\"test-model\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"ok\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":2,\"total_tokens\":3}}",
                Encoding.UTF8,
                "application/json")
        });
        var context = new TestOpenAIContext { MaxOutputTokens = 512 };
        var llm = new OpenAICompatible(new TestHttpClientFactory(handler), context, NullLogger<OpenAICompatible>.Instance);

        var result = await llm.ChatAsync(
            new LLMTurnContext { Model = "test-model", MaxTokens = 4096 },
            new List<ChatMessage> { ChatMessage.User("hello") },
            Array.Empty<ToolDefinition>());

        using var doc = JsonDocument.Parse(handler.CapturedBody);
        Assert.Equal(512, doc.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.True(result.Success);
        Assert.Equal("stop", result.FinishReason);
        Assert.Equal("req-1", result.RequestId);
    }

    [Fact]
    public async Task ChatAsync_ReturnsFailureResultForHttpErrors()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            ReasonPhrase = "Too Many Requests",
            Content = new StringContent("{\"error\":\"rate limited\"}", Encoding.UTF8, "application/json")
        });
        var llm = new OpenAICompatible(
            new TestHttpClientFactory(handler),
            new TestOpenAIContext(),
            NullLogger<OpenAICompatible>.Instance);

        var result = await llm.ChatAsync(
            new LLMTurnContext { Model = "test-model" },
            new List<ChatMessage> { ChatMessage.User("hello") },
            Array.Empty<ToolDefinition>());

        Assert.False(result.Success);
        Assert.Contains("429", result.ErrorMessage);
        Assert.Contains("rate limited", result.ErrorMessage);
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public string CapturedBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responseFactory(request);
        }
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }
}
