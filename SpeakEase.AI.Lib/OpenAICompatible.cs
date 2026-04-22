using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// 基于 OpenAI-compatible chat completions 协议的 <see cref="IChatCompatible"/> 实现。
    /// 支持：流式/非流式、工具调用、自定义鉴权头。
    /// 直接实现 ILLMStrategy，封装 HTTP 通信 + 协议解析 + 流式 delta 累积全部逻辑。
    /// </summary>
    public sealed class OpenAICompatible(
        IHttpClientFactory httpClientFactory,
        IOpenAIContext context,
        ILogger<OpenAICompatible> logger) : IChatCompatible
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly IOpenAIContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly ILogger<OpenAICompatible> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <inheritdoc />
        public async Task<LLMTurnResult> ChatAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            await _context.ResolveAsync(cancellationToken);

            var request = BuildRequest(context, messages, tools, stream: false);

            _logger.LogDebug(
                "ChatAsync 开始: Model={Model}, Messages={MsgCount}, Tools={ToolCount}",
                request.Model, request.Messages?.Count ?? 0, request.Tools?.Count ?? 0);

            var httpClient = CreateConfiguredClient();

            using var response = await httpClient.PostAsJsonAsync(
                "chat/completions", request, JsonOptions, cancellationToken);

            var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "ChatAsync LLM 返回 HTTP 错误: {StatusCode} {ReasonPhrase}\n{Body}",
                    (int)response.StatusCode, response.ReasonPhrase, rawResponse);
                throw new InvalidOperationException(
                    $"LLM 调用失败: {(int)response.StatusCode} {response.ReasonPhrase}\n{rawResponse}");
            }

            var result = JsonSerializer.Deserialize<ChatCompletionResponse>(rawResponse, JsonOptions)
                ?? throw new InvalidOperationException("LLM 响应反序列化失败。");

            var firstChoice = result.Choices?.FirstOrDefault();
            var hasToolCalls = firstChoice?.Message?.ToolCalls?.Any() ?? false;

            _logger.LogInformation(
                "ChatAsync 完成: Model={Model}, FinishReason={FinishReason}, HasToolCalls={HasToolCalls}, " +
                "Tokens={PromptTokens}+{CompletionTokens}={TotalTokens}",
                result.Model,
                firstChoice?.FinishReason ?? "(null)",
                hasToolCalls,
                result.Usage?.PromptTokens,
                result.Usage?.CompletionTokens,
                result.Usage?.TotalTokens);

            return new LLMTurnResult
            {
                Content = firstChoice?.Message?.Content ?? string.Empty,
                ToolCalls = firstChoice?.Message?.ToolCalls,
                Model = result.Model,
                Usage = result.Usage
            };
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            await _context.ResolveAsync(cancellationToken);

            var request = BuildRequest(context, messages, tools, stream: true);

            _logger.LogDebug(
                "StreamAsync 开始: Model={Model}, Messages={MsgCount}, Tools={ToolCount}",
                request.Model, request.Messages?.Count ?? 0, request.Tools?.Count ?? 0);

            var httpClient = CreateConfiguredClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };

            using var response = await httpClient.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "StreamAsync LLM 返回 HTTP 错误: {StatusCode} {ReasonPhrase}\n{Body}",
                    (int)response.StatusCode, response.ReasonPhrase, error);
                throw new InvalidOperationException(
                    $"LLM 流式调用失败: {(int)response.StatusCode} {response.ReasonPhrase}\n{error}");
            }

            _logger.LogInformation("StreamAsync 流式连接已建立: Model={Model}", request.Model);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // 累积流式内容
            var contentBuilder = new StringBuilder();
            var toolCallAccumulators = new Dictionary<int, ToolCallAccumulator>();
            string finishReason = null;
            string responseModel = context.Model;
            UsageInfo usage = null;

            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;

                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
                    continue;

                var data = line[5..].Trim();
                if (data == "[DONE]")
                    break;

                var chunk = JsonSerializer.Deserialize<ChatCompletionStreamChunk>(data, JsonOptions);
                if (chunk is null)
                    continue;

                if (!string.IsNullOrEmpty(chunk.Model))
                    responseModel = chunk.Model;

                if (chunk.Usage != null)
                {
                    usage ??= new UsageInfo();
                    usage.PromptTokens += chunk.Usage.PromptTokens;
                    usage.CompletionTokens += chunk.Usage.CompletionTokens;
                    usage.TotalTokens += chunk.Usage.TotalTokens;
                }

                var choice = chunk.Choices?.FirstOrDefault();
                if (choice == null)
                    continue;

                if (!string.IsNullOrEmpty(choice.FinishReason))
                    finishReason = choice.FinishReason;

                var delta = choice.Delta;
                if (delta == null)
                    continue;

                // 内容增量
                if (!string.IsNullOrEmpty(delta.Content))
                {
                    contentBuilder.Append(delta.Content);
                    yield return new LLMTurnChunk
                    {
                        Type = "content",
                        Content = delta.Content
                    };
                }

                // 工具调用增量
                if (delta.ToolCalls != null)
                {
                    foreach (var toolCallDelta in delta.ToolCalls)
                    {
                        StreamToolCallHelper.MergeDelta(toolCallAccumulators, toolCallDelta);

                        yield return new LLMTurnChunk
                        {
                            Type = "tool_call",
                            ToolCallDelta = new ToolCallDelta
                            {
                                Index = toolCallDelta.Index,
                                Id = toolCallDelta.Id,
                                Type = toolCallDelta.Type,
                                Function = new FunctionCallDelta
                                {
                                    Name = toolCallDelta.Function?.Name,
                                    Arguments = toolCallDelta.Function?.Arguments
                                }
                            }
                        };
                    }
                }
            }

            // 流结束，输出本轮完整结果
            var hasToolCalls = toolCallAccumulators.Count > 0 &&
                (finishReason == "tool_calls" || finishReason == null && toolCallAccumulators.Count > 0);

            _logger.LogInformation("StreamAsync 流式结束: Model={Model}, HasToolCalls={HasToolCalls}, AccumulatedTools={AccumulatedTools}",
                responseModel, hasToolCalls, toolCallAccumulators.Count);

            yield return new LLMTurnChunk
            {
                Type = "done",
                TurnResult = new LLMTurnResult
                {
                    Content = contentBuilder.ToString(),
                    ToolCalls = hasToolCalls ? StreamToolCallHelper.ToToolCalls(toolCallAccumulators) : null,
                    Model = responseModel,
                    Usage = usage
                }
            };
        }



        private HttpClient CreateConfiguredClient()
        {
            var client = _httpClientFactory.CreateClient();
            var baseUrl = _context.Url;

            if (!baseUrl.EndsWith('/'))
                baseUrl += "/";

            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(_context.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _context.ApiKey);
            }

            _logger.LogDebug(
                "HttpClient 已配置: BaseAddress={BaseAddress}, Timeout={Timeout}s",client.BaseAddress, client.Timeout.TotalSeconds);

            return client;
        }

        /// <summary>
        /// 构造 OpenAI ChatCompletionRequest
        /// </summary>
        private ChatCompletionRequest BuildRequest(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            bool stream) => new()
        {
            Model = string.IsNullOrWhiteSpace(context.Model) ? _context.Model : context.Model,
            Messages = messages,
            Tools = tools?.Count > 0 ? tools.ToList() : null,
            ToolChoice = context.ToolChoice,
            Temperature = context.Temperature,
            MaxTokens = context.MaxTokens,
            Stream = stream
        };
    }
}
