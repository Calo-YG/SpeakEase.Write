using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using AINWZ.Infrastructure.LLM.Options;
using Microsoft.Extensions.Options;

namespace AINWZ.Infrastructure.LLM.Providers;

/// <summary>
/// 基于 OpenAI-compatible chat completions 协议的 LLM Provider。
/// </summary>
/// <remarks>
/// 初始化 Provider。
/// </remarks>
public sealed class OpenAICompatibleLLMProvider(HttpClient httpClient, IOptions<LLMOptions> options) : ILLMProvider
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly LLMOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<LLMChatResponse> ChatAsync(LLMChatRequest request, CancellationToken cancellationToken = default)
    {
        EnsureRequestIsValid(request);

        Exception lastException = null;
        var modelCandidates = ResolveModelCandidates(request);
        var messages = BuildMessages(request);

        foreach (var model in modelCandidates)
        {
            var payload = BuildRequestPayload(request, messages, model, stream: false);

            try
            {
                using var response = await httpClient.PostAsJsonAsync("chat/completions", payload, JsonSerializerOptions, cancellationToken);
                var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"LLM 调用失败: {(int)response.StatusCode} {response.ReasonPhrase}\n{rawResponse}");
                }

                var result = JsonSerializer.Deserialize<OpenAICompatibleChatCompletionResponse>(rawResponse, JsonSerializerOptions)
                             ?? throw new InvalidOperationException("LLM 响应反序列化失败。");

                var firstChoice = result.Choices.FirstOrDefault()
                                  ?? throw new InvalidOperationException("LLM 响应中未包含可用结果。");

                var finalModel = result.Model ?? payload.Model;

                return new LLMChatResponse
                {
                    PrimaryModel = modelCandidates[0],
                    FinalModel = finalModel,
                    Model = finalModel,
                    UsedFallback = model != modelCandidates[0],
                    FallbackModel = model != modelCandidates[0] ? model : null,
                    Content = firstChoice.Message?.Content ?? string.Empty,
                    RawResponse = rawResponse,
                    RequestId = result.Id,
                    PromptTokens = result.Usage?.PromptTokens,
                    CompletionTokens = result.Usage?.CompletionTokens,
                    TotalTokens = result.Usage?.TotalTokens,
                    FinishReason = firstChoice.FinishReason,
                    ToolCalls = firstChoice.Message?.ToolCalls?.Select(MapToolCall).ToList() ?? new List<LLMToolCall>()
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastException = exception;
            }
        }

        throw new InvalidOperationException("所有 LLM 模型均调用失败。", lastException);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<LLMStreamEvent> StreamAsync(LLMChatRequest request, CancellationToken cancellationToken = default)
    {
        EnsureRequestIsValid(request);
        return StreamInternalAsync(request, cancellationToken);
    }

    internal static void ConfigureHttpClient(HttpClient client, LLMOptions options)
    {
        client.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(options.ApiKeyHeaderName))
        {
            var headerValue = string.IsNullOrWhiteSpace(options.ApiKeyHeaderPrefix)
                ? options.ApiKey
                : $"{options.ApiKeyHeaderPrefix} {options.ApiKey}";

            client.DefaultRequestHeaders.Remove(options.ApiKeyHeaderName);
            client.DefaultRequestHeaders.Add(options.ApiKeyHeaderName, headerValue);
        }
        else if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    }

    private async IAsyncEnumerable<LLMStreamEvent> StreamInternalAsync(LLMChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Exception lastException = null;
        var modelCandidates = ResolveModelCandidates(request);
        var messages = BuildMessages(request);
        var primaryModel = modelCandidates[0];

        for (var index = 0; index < modelCandidates.Count; index++)
        {
            var model = modelCandidates[index];
            var usedFallback = index > 0;
            var payload = BuildRequestPayload(request, messages, model, stream: true);

            if (usedFallback)
            {
                yield return new LLMStreamEvent
                {
                    Type = "fallback",
                    Model = model,
                    FromModel = primaryModel,
                    ToModel = model,
                    UsedFallback = true
                };
            }

            var attempt = await TryStartStreamAttemptAsync(payload, cancellationToken);
            if (!attempt.Started)
            {
                lastException = attempt.Exception;

                if (index == modelCandidates.Count - 1)
                {
                    yield return BuildErrorEvent(attempt.Exception, model, primaryModel, usedFallback, attempt.RequestId, "llm_stream_failed");
                }

                continue;
            }

            await foreach (var streamEvent in ConsumeStreamAttemptAsync(attempt, primaryModel, usedFallback, cancellationToken))
            {
                yield return streamEvent;
            }

            if (attempt.CompletedSuccessfully)
            {
                yield return new LLMStreamEvent
                {
                    Type = "done",
                    RequestId = attempt.RequestId,
                    Model = model,
                    UsedFallback = usedFallback,
                    FromModel = usedFallback ? primaryModel : null,
                    ToModel = usedFallback ? model : null,
                    FinishReason = attempt.FinishReason,
                    ToolCalls = attempt.ToolCalls.Values.OrderBy(item => item.Index).Select(MapToolCall).ToList()
                };

                yield break;
            }

            lastException = attempt.Exception;

            if (index == modelCandidates.Count - 1)
            {
                yield return BuildErrorEvent(
                    attempt.Exception,
                    model,
                    primaryModel,
                    usedFallback,
                    attempt.RequestId,
                    attempt.EmittedChunk ? "llm_stream_interrupted" : "llm_stream_failed");
            }
        }

        if (lastException is not null && modelCandidates.Count == 0)
        {
            yield return BuildErrorEvent(lastException, null, null, false, null, "llm_all_models_failed");
        }
    }

    private async Task<StreamAttemptState> TryStartStreamAttemptAsync(OpenAICompatibleChatRequest payload, CancellationToken cancellationToken)
    {
        try
        {
            var enumerator = ReadStreamEventsAsync(payload, cancellationToken).GetAsyncEnumerator(cancellationToken);
            return new StreamAttemptState(enumerator);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new StreamAttemptState(exception)
            {
                CompletedSuccessfully = false,
                EmittedChunk = false
            };
        }
    }

    private async IAsyncEnumerable<LLMStreamEvent> ConsumeStreamAttemptAsync(
        StreamAttemptState attempt,
        string primaryModel,
        bool usedFallback,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (attempt.Enumerator is null)
        {
            yield break;
        }

        await using var enumerator = attempt.Enumerator;

        while (true)
        {
            bool hasNext;

            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                attempt.Exception = exception;
                attempt.CompletedSuccessfully = false;
                yield break;
            }

            if (!hasNext)
            {
                attempt.CompletedSuccessfully = true;
                yield break;
            }

            var streamEvent = enumerator.Current;
            if (!string.IsNullOrWhiteSpace(streamEvent.RequestId))
            {
                attempt.RequestId = streamEvent.RequestId;
            }

            if (!string.IsNullOrWhiteSpace(streamEvent.FinishReason))
            {
                attempt.FinishReason = streamEvent.FinishReason;
            }

            if (streamEvent.ToolCallDelta is not null)
            {
                MergeToolCallDelta(attempt.ToolCalls, streamEvent.ToolCallDelta);
            }

            if (streamEvent.Type == "chunk")
            {
                attempt.EmittedChunk = true;
            }

            streamEvent.UsedFallback = usedFallback;
            if (usedFallback)
            {
                streamEvent.FromModel ??= primaryModel;
                streamEvent.ToModel ??= streamEvent.Model;
            }

            yield return streamEvent;
        }
    }

    private static void MergeToolCallDelta(IDictionary<int, StreamToolCallState> states, LLMToolCallDelta delta)
    {
        if (!states.TryGetValue(delta.Index, out var state))
        {
            state = new StreamToolCallState { Index = delta.Index };
            states[delta.Index] = state;
        }

        if (!string.IsNullOrWhiteSpace(delta.Id))
        {
            state.Id = delta.Id;
        }

        if (!string.IsNullOrWhiteSpace(delta.Type))
        {
            state.Type = delta.Type;
        }

        if (!string.IsNullOrWhiteSpace(delta.Name))
        {
            state.Name ??= string.Empty;
            state.Name += delta.Name;
        }

        if (!string.IsNullOrWhiteSpace(delta.Arguments))
        {
            state.Arguments ??= string.Empty;
            state.Arguments += delta.Arguments;
        }
    }

    private static LLMToolCall MapToolCall(StreamToolCallState state)
    {
        return new LLMToolCall
        {
            Id = state.Id,
            Type = string.IsNullOrWhiteSpace(state.Type) ? "function" : state.Type,
            Function = new LLMToolFunctionCall
            {
                Name = state.Name ?? string.Empty,
                Arguments = state.Arguments ?? string.Empty
            }
        };
    }

    private static LLMStreamEvent BuildErrorEvent(Exception exception, string model, string primaryModel, bool usedFallback, string requestId, string errorCode)
    {
        return new LLMStreamEvent
        {
            Type = "error",
            RequestId = requestId,
            Model = model,
            UsedFallback = usedFallback,
            FromModel = usedFallback ? primaryModel : null,
            ToModel = usedFallback ? model : null,
            ErrorCode = errorCode,
            ErrorMessage = exception?.Message ?? "未知流式错误。"
        };
    }

    private static void EnsureRequestIsValid(LLMChatRequest request)
    {
        if (request.Messages.Count == 0 && string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            throw new InvalidOperationException("LLM 请求至少需要一条消息或系统提示词。");
        }
    }

    private List<string> ResolveModelCandidates(LLMChatRequest request)
    {
        var models = new List<string>();
        var primaryModel = string.IsNullOrWhiteSpace(request.Model) ? _options.DefaultModel : request.Model!;

        if (!string.IsNullOrWhiteSpace(primaryModel))
        {
            models.Add(primaryModel);
        }

        foreach (var model in request.FallbackModels.Concat(_options.FallbackModels))
        {
            if (!string.IsNullOrWhiteSpace(model) && !models.Contains(model, StringComparer.OrdinalIgnoreCase))
            {
                models.Add(model);
            }
        }

        if (models.Count == 0)
        {
            throw new InvalidOperationException("未配置任何可用的 LLM 模型。");
        }

        return models;
    }

    private static List<OpenAICompatibleChatMessage> BuildMessages(LLMChatRequest request)
    {
        var messages = new List<OpenAICompatibleChatMessage>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new OpenAICompatibleChatMessage("system", request.SystemPrompt, null, null, null));
        }

        messages.AddRange(request.Messages.Select(message => new OpenAICompatibleChatMessage(
            message.Role,
            message.Content,
            message.Name,
            message.ToolCallId,
            message.ToolCalls?.Select(MapToolCall).ToList())));
        return messages;
    }

    private async IAsyncEnumerable<LLMStreamEvent> ReadStreamEventsAsync(OpenAICompatibleChatRequest payload, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload, options: JsonSerializerOptions)
        };

        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"LLM 流式调用失败: {(int)response.StatusCode} {response.ReasonPhrase}\n{error}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data == "[DONE]")
            {
                break;
            }

            var chunk = JsonSerializer.Deserialize<OpenAICompatibleStreamResponse>(data, JsonSerializerOptions);
            if (chunk is null)
            {
                continue;
            }

            var choice = chunk.Choices.FirstOrDefault();
            if (choice is null)
            {
                continue;
            }

            var delta = choice.Delta;

            if (!string.IsNullOrEmpty(delta?.Content))
            {
                yield return new LLMStreamEvent
                {
                    Type = "chunk",
                    RequestId = chunk.Id,
                    Model = payload.Model,
                    Content = delta.Content
                };
            }

            if (delta?.ToolCalls is not null)
            {
                foreach (var toolCall in delta.ToolCalls)
                {
                    yield return new LLMStreamEvent
                    {
                        Type = "tool_call_delta",
                        RequestId = chunk.Id,
                        Model = payload.Model,
                        ToolCallDelta = new LLMToolCallDelta
                        {
                            Index = toolCall.Index,
                            Id = toolCall.Id,
                            Type = toolCall.Type,
                            Name = toolCall.Function?.Name,
                            Arguments = toolCall.Function?.Arguments
                        }
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(choice.FinishReason))
            {
                yield return new LLMStreamEvent
                {
                    Type = "finish",
                    RequestId = chunk.Id,
                    Model = payload.Model,
                    FinishReason = choice.FinishReason
                };
            }
        }
    }

    private static OpenAICompatibleChatRequest BuildRequestPayload(LLMChatRequest request, List<OpenAICompatibleChatMessage> messages, string model, bool stream)
    {
        return new OpenAICompatibleChatRequest
        {
            Model = model,
            Messages = messages,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Stream = stream,
            ResponseFormat = request.UseJsonMode ? new OpenAICompatibleResponseFormat("json_object") : null,
            Tools = request.Tools.Select(MapToolDefinition).ToList(),
            ToolChoice = MapToolChoice(request.ToolChoice)
        };
    }

    private sealed class StreamAttemptState
    {
        public StreamAttemptState(IAsyncEnumerator<LLMStreamEvent> enumerator)
        {
            Enumerator = enumerator;
            Started = true;
        }

        public StreamAttemptState(Exception exception)
        {
            Exception = exception;
            Started = false;
        }

        public bool Started { get; }

        public IAsyncEnumerator<LLMStreamEvent> Enumerator { get; }

        public Exception Exception { get; set; }

        public string RequestId { get; set; }

        public bool EmittedChunk { get; set; }

        public bool CompletedSuccessfully { get; set; }

        public string FinishReason { get; set; }

        public Dictionary<int, StreamToolCallState> ToolCalls { get; } = new();
    }

    private sealed class StreamToolCallState
    {
        public int Index { get; set; }

        public string Id { get; set; }

        public string Type { get; set; }

        public string Name { get; set; }

        public string Arguments { get; set; }
    }

    private sealed class OpenAICompatibleChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenAICompatibleChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public decimal? Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("response_format")]
        public OpenAICompatibleResponseFormat ResponseFormat { get; set; }

        [JsonPropertyName("tools")]
        public List<OpenAICompatibleToolDefinition> Tools { get; set; } = new();

        [JsonPropertyName("tool_choice")]
        public object ToolChoice { get; set; }
    }

    private sealed record OpenAICompatibleChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("tool_call_id")] string ToolCallId,
        [property: JsonPropertyName("tool_calls")] List<OpenAICompatibleToolCall> ToolCalls);

    private sealed record OpenAICompatibleResponseFormat([property: JsonPropertyName("type")] string Type);

    private sealed class OpenAICompatibleChatCompletionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("choices")]
        public List<OpenAICompatibleChoice> Choices { get; set; } = new();

        [JsonPropertyName("usage")]
        public OpenAICompatibleUsage Usage { get; set; }
    }

    private sealed class OpenAICompatibleChoice
    {
        [JsonPropertyName("message")]
        public OpenAICompatibleResponseMessage Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }
    }

    private sealed class OpenAICompatibleResponseMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<OpenAICompatibleToolCall> ToolCalls { get; set; }
    }

    private sealed class OpenAICompatibleUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }

    private sealed class OpenAICompatibleStreamResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("choices")]
        public List<OpenAICompatibleStreamChoice> Choices { get; set; } = new();
    }

    private sealed class OpenAICompatibleStreamChoice
    {
        [JsonPropertyName("delta")]
        public OpenAICompatibleStreamDelta Delta { get; set; }

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }
    }

    private sealed class OpenAICompatibleStreamDelta
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<OpenAICompatibleStreamToolCall> ToolCalls { get; set; }
    }

    private sealed class OpenAICompatibleToolDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenAICompatibleToolFunctionDefinition Function { get; set; } = new();
    }

    private sealed class OpenAICompatibleToolFunctionDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("parameters")]
        public object Parameters { get; set; }
    }

    private sealed class OpenAICompatibleToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenAICompatibleToolFunctionCall Function { get; set; } = new();
    }

    private sealed class OpenAICompatibleToolFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;
    }

    private sealed class OpenAICompatibleStreamToolCall
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("function")]
        public OpenAICompatibleStreamToolFunctionCall Function { get; set; }
    }

    private sealed class OpenAICompatibleStreamToolFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; }
    }

    private static OpenAICompatibleToolDefinition MapToolDefinition(LLMToolDefinition tool)
    {
        return new OpenAICompatibleToolDefinition
        {
            Type = tool.Type,
            Function = new OpenAICompatibleToolFunctionDefinition
            {
                Name = tool.Function.Name,
                Description = tool.Function.Description,
                Parameters = tool.Function.Parameters
            }
        };
    }

    private static object MapToolChoice(LLMToolChoice toolChoice)
    {
        if (toolChoice is null)
        {
            return null;
        }

        if (!string.Equals(toolChoice.Type, "function", StringComparison.OrdinalIgnoreCase))
        {
            return toolChoice.Type;
        }

        return new
        {
            type = "function",
            function = new
            {
                name = toolChoice.Function?.Name
            }
        };
    }

    private static LLMToolCall MapToolCall(OpenAICompatibleToolCall toolCall)
    {
        return new LLMToolCall
        {
            Id = toolCall.Id,
            Type = toolCall.Type,
            Function = new LLMToolFunctionCall
            {
                Name = toolCall.Function.Name,
                Arguments = toolCall.Function.Arguments
            }
        };
    }

    private static OpenAICompatibleToolCall MapToolCall(LLMToolCall toolCall)
    {
        return new OpenAICompatibleToolCall
        {
            Id = toolCall.Id,
            Type = toolCall.Type,
            Function = new OpenAICompatibleToolFunctionCall
            {
                Name = toolCall.Function.Name,
                Arguments = toolCall.Function.Arguments
            }
        };
    }
}
