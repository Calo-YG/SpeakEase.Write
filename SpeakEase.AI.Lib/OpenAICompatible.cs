using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Options;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// 基于 OpenAI-compatible chat completions 协议的 IAgentLLMBackend 实现。
    /// 支持：模型回退、流式/非流式、工具调用、自定义鉴权头。
    /// </summary>
    public sealed class OpenAICompatible: IChatCompatible
    {
        /// <summary>
        /// JSON 序列化选项，Web 默认 + 忽略 null 值。
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// HTTP 客户端工厂，用于创建配置好的 HttpClient 实例。
        /// </summary>
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// 配置解析委托，支持动态刷新 LLM 配置（如模型切换、密钥轮换）。
        /// </summary>
        private readonly Func<Task<OpenAIOptions>> _optionsResolver;

        /// <summary>
        /// 通过 IHttpClientFactory + 配置解析委托创建。
        /// </summary>
        public OpenAICompatible(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        /// <inheritdoc />
        public async Task<AgentResponse> CompleteAsync(AgentRequest request, CancellationToken cancellationToken = default)
        {
            var options = await _optionsResolver();
            using var httpClient = CreateConfiguredClient(options);

            var modelCandidates = ResolveModelCandidates(request, options);
            var messages = BuildMessages(request);

            Exception lastException = null;

            foreach (var (model, index) in modelCandidates.Select((m, i) => (m, i)))
            {
                var payload = BuildRequestPayload(request, messages, model, stream: false);

                try
                {
                    using var httpResponse = await httpClient.PostAsJsonAsync("chat/completions", payload, JsonOptions, cancellationToken);

                    var rawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException($"LLM 调用失败: {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}\n{rawResponse}");
                    }

                    var result = JsonSerializer.Deserialize<ChatCompletionResponse>(rawResponse, JsonOptions)
                                 ?? throw new InvalidOperationException("LLM 响应反序列化失败。");

                    var firstChoice = result.Choices.FirstOrDefault()
                                      ?? throw new InvalidOperationException("LLM 响应中未包含可用结果。");

                    var finalModel = result.Model ?? payload.Model;

                    return new AgentResponse
                    {
                        Model = finalModel,
                        Content = firstChoice.Message?.Content ?? string.Empty,
                        StopReason = firstChoice.FinishReason,
                        ToolCalls = firstChoice.Message?.ToolCalls?.Select(MapToolCall).ToList() ?? new List<ToolCall>()
                    };
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastException = ex;
                }
            }

            throw new InvalidOperationException("所有 LLM 模型均调用失败。", lastException);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<AgentStreamChunk> StreamAsync(
            AgentRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var options = await _optionsResolver();
            using var httpClient = CreateConfiguredClient(options);

            var modelCandidates = ResolveModelCandidates(request, options);
            var messages = BuildMessages(request);

            // 使用第一个模型候选直接流式输出（IAsyncEnumerable 不允许 try-catch 中 yield）
            // 模型回退逻辑由 CompleteAsync（非流式）保证，流式场景失败直接抛异常
            var model = modelCandidates[0];
            var payload = BuildRequestPayload(request, messages, model, stream: true);

            await foreach (var chunk in ReadStreamChunksAsync(payload, httpClient, cancellationToken).WithCancellation(cancellationToken))
            {
                yield return chunk;
            }
        }

        #region HTTP 请求构建

        /// <summary>
        /// 根据配置创建 HttpClient，设置 BaseAddress、超时、鉴权头。
        /// </summary>
        private HttpClient CreateConfiguredClient(OpenAIOptions options)
        {
            var client = _httpClientFactory.CreateClient();
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

            return client;
        }

        /// <summary>
        /// 解析模型候选列表：请求级 Model 优先，其次默认模型，最后追加备用模型。
        /// </summary>
        private static List<string> ResolveModelCandidates(AgentRequest request, OpenAIOptions options)
        {
            var models = new List<string>();
            var primaryModel = string.IsNullOrWhiteSpace(request.Model) ? options.DefaultModel : request.Model!;

            if (!string.IsNullOrWhiteSpace(primaryModel))
            {
                models.Add(primaryModel);
            }

            foreach (var model in options.FallbackModels)
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

        /// <summary>
        /// 将 AgentRequest.Messages 转换为 OpenAI API 格式的消息列表。
        /// </summary>
        private static List<ApiChatMessage> BuildMessages(AgentRequest request)
        {
            var messages = new List<ApiChatMessage>();

            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                messages.Add(new ApiChatMessage("system", request.SystemPrompt, null, null, null));
            }

            messages.AddRange(request.Messages.Select(m => new ApiChatMessage(
                m.Role, m.Content, m.Name, m.ToolCallId,
                m.ToolCalls?.Select(MapToolCallToApi).ToList())));

            return messages;
        }

        /// <summary>
        /// 构建 OpenAI chat/completions 请求体。
        /// </summary>
        private static ApiChatRequest BuildRequestPayload(AgentRequest request, List<ApiChatMessage> messages, string model, bool stream)
        {
            return new ApiChatRequest
            {
                Model = model,
                Messages = messages,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Stream = stream,
                Tools = request.Tools.Select(MapToolDefinitionToApi).ToList(),
                ToolChoice = request.EnableToolDispatch ? null : "none"
            };
        }

        #endregion

        #region 流式解析

        /// <summary>
        /// 以 SSE 方式读取 LLM 流式响应，逐块解析为 AgentStreamChunk。
        /// </summary>
        private static async IAsyncEnumerable<AgentStreamChunk> ReadStreamChunksAsync(
            ApiChatRequest payload,
            HttpClient httpClient,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };

            using var httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var error = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"LLM 流式调用失败: {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}\n{error}");
            }

            await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, encoding: Encoding.UTF8);

            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) break;

                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:")) continue;

                var data = line[5..].Trim();
                if (data == "[DONE]") break;

                var chunk = JsonSerializer.Deserialize<ApiStreamResponse>(data, JsonOptions);
                if (chunk is null) continue;

                var choice = chunk.Choices.FirstOrDefault();
                if (choice is null) continue;

                var delta = choice.Delta;

                if (!string.IsNullOrEmpty(delta?.Content))
                {
                    yield return new AgentStreamChunk
                    {
                        Type = "content",
                        ContentDelta = delta.Content
                    };
                }

                if (delta?.ToolCalls is not null)
                {
                    foreach (var toolCall in delta.ToolCalls)
                    {
                        yield return new AgentStreamChunk
                        {
                            Type = "tool_call_delta",
                            ToolCallDelta = new ToolCallDelta
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
                    yield return new AgentStreamChunk
                    {
                        Type = "finish",
                        FinishReason = choice.FinishReason
                    };
                }
            }
        }

        #endregion

        #region 类型映射

        /// <summary>
        /// 将 API 层 ToolCall 映射为领域层 ToolCall。
        /// </summary>
        private static ToolCall MapToolCall(ApiToolCall toolCall)
        {
            return new ToolCall
            {
                Id = toolCall.Id,
                Type = toolCall.Type,
                Function = new ToolFunctionCall
                {
                    Name = toolCall.Function?.Name ?? string.Empty,
                    Arguments = toolCall.Function?.Arguments ?? string.Empty
                }
            };
        }

        /// <summary>
        /// 将领域层 ToolCall 映射为 API 层 ToolCall。
        /// </summary>
        private static ApiToolCall MapToolCallToApi(ToolCall toolCall)
        {
            return new ApiToolCall
            {
                Id = toolCall.Id,
                Type = toolCall.Type,
                Function = new ApiToolFunctionCall
                {
                    Name = toolCall.Function?.Name ?? string.Empty,
                    Arguments = toolCall.Function?.Arguments ?? string.Empty
                }
            };
        }

        /// <summary>
        /// 将领域层 ToolDefinition 映射为 API 层 ToolDefinition。
        /// </summary>
        private static ApiToolDefinition MapToolDefinitionToApi(ToolDefinition tool)
        {
            return new ApiToolDefinition
            {
                Type = tool.Type,
                Function = new ApiToolFunctionDefinition
                {
                    Name = tool.Function?.Name,
                    Description = tool.Function?.Description,
                    Parameters = tool.Function?.Parameters
                }
            };
        }

        #endregion

        #region OpenAI API 内部模型

        /// <summary>OpenAI chat/completions 请求体。</summary>
        private sealed class ApiChatRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("messages")] public List<ApiChatMessage> Messages { get; set; } = new();
            [JsonPropertyName("temperature")] public decimal? Temperature { get; set; }
            [JsonPropertyName("max_tokens")] public int? MaxTokens { get; set; }
            [JsonPropertyName("stream")] public bool Stream { get; set; }
            [JsonPropertyName("tools")] public List<ApiToolDefinition> Tools { get; set; } = new();
            [JsonPropertyName("tool_choice")] public object ToolChoice { get; set; }
        }

        /// <summary>OpenAI chat message 格式。</summary>
        private sealed record ApiChatMessage(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("content")] string Content,
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("tool_call_id")] string ToolCallId,
            [property: JsonPropertyName("tool_calls")] List<ApiToolCall> ToolCalls);

        /// <summary>API 层工具定义。</summary>
        private sealed class ApiToolDefinition
        {
            [JsonPropertyName("type")] public string Type { get; set; } = "function";
            [JsonPropertyName("function")] public ApiToolFunctionDefinition Function { get; set; }
        }

        /// <summary>API 层工具函数定义。</summary>
        private sealed class ApiToolFunctionDefinition
        {
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("description")] public string Description { get; set; }
            [JsonPropertyName("parameters")] public object Parameters { get; set; }
        }

        /// <summary>API 层工具调用。</summary>
        private sealed class ApiToolCall
        {
            [JsonPropertyName("id")] public string Id { get; set; }
            [JsonPropertyName("type")] public string Type { get; set; } = "function";
            [JsonPropertyName("function")] public ApiToolFunctionCall Function { get; set; }
        }

        /// <summary>API 层工具函数调用。</summary>
        private sealed class ApiToolFunctionCall
        {
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("arguments")] public string Arguments { get; set; } = string.Empty;
        }

        /// <summary>OpenAI chat/completions 非流式响应。</summary>
        private sealed class ChatCompletionResponse
        {
            [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
            [JsonPropertyName("model")] public string Model { get; set; }
            [JsonPropertyName("choices")] public List<ChatCompletionChoice> Choices { get; set; } = new();
        }

        /// <summary>非流式响应选项。</summary>
        private sealed class ChatCompletionChoice
        {
            [JsonPropertyName("message")] public ChatCompletionMessage Message { get; set; }
            [JsonPropertyName("finish_reason")] public string FinishReason { get; set; }
        }

        /// <summary>非流式响应消息。</summary>
        private sealed class ChatCompletionMessage
        {
            [JsonPropertyName("content")] public string Content { get; set; }
            [JsonPropertyName("tool_calls")] public List<ApiToolCall> ToolCalls { get; set; }
        }

        /// <summary>OpenAI chat/completions 流式响应。</summary>
        private sealed class ApiStreamResponse
        {
            [JsonPropertyName("id")] public string Id { get; set; }
            [JsonPropertyName("choices")] public List<ApiStreamChoice> Choices { get; set; } = new();
        }

        /// <summary>流式响应选项。</summary>
        private sealed class ApiStreamChoice
        {
            [JsonPropertyName("delta")] public ApiStreamDelta Delta { get; set; }
            [JsonPropertyName("finish_reason")] public string FinishReason { get; set; }
        }

        /// <summary>流式响应增量内容。</summary>
        private sealed class ApiStreamDelta
        {
            [JsonPropertyName("content")] public string Content { get; set; }
            [JsonPropertyName("tool_calls")] public List<ApiStreamToolCall> ToolCalls { get; set; }
        }

        /// <summary>流式响应工具调用增量。</summary>
        private sealed class ApiStreamToolCall
        {
            [JsonPropertyName("index")] public int Index { get; set; }
            [JsonPropertyName("id")] public string Id { get; set; }
            [JsonPropertyName("type")] public string Type { get; set; }
            [JsonPropertyName("function")] public ApiStreamToolFunctionCall Function { get; set; }
        }

        /// <summary>流式响应工具函数调用增量。</summary>
        private sealed class ApiStreamToolFunctionCall
        {
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("arguments")] public string Arguments { get; set; }
        }

        #endregion
    }
}
