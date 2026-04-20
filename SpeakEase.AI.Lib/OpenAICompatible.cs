using Microsoft.Extensions.Logging;
using SpeakEase.AI.Lib.Contract;
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
    /// 仅依赖 <see cref="OpenAIModel"/> 线路格式模型，不耦合任何 Agent 域模型。
    /// </summary>
    public sealed class OpenAICompatible : IChatCompatible
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOpenAIContext _context;
        private readonly ILogger<OpenAICompatible> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public OpenAICompatible(
            IHttpClientFactory httpClientFactory,
            IOpenAIContext context,
            ILogger<OpenAICompatible> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ChatCompletionResponse> ChatAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Model))
                request.Model = _context.Model;

            _logger.LogDebug(
                "ChatAsync 开始: Model={Model}, Messages={MsgCount}, Tools={ToolCount}",
                request.Model, request.Messages?.Count ?? 0, request.Tools?.Count ?? 0);

            using var httpClient = CreateConfiguredClient();

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

            return result;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<ChatCompletionStreamChunk> StreamAsync(
            ChatCompletionRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Model))
                request.Model = _context.Model;
            request.Stream = true;

            _logger.LogDebug(
                "StreamAsync 开始: Model={Model}, Messages={MsgCount}, Tools={ToolCount}",
                request.Model, request.Messages?.Count ?? 0, request.Tools?.Count ?? 0);

            using var httpClient = CreateConfiguredClient();
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
            var emittedAny = false;
            var toolCallAccumulators = new Dictionary<int, ToolCallAccumulator>();

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

                if (string.IsNullOrWhiteSpace(chunk.Model))
                    chunk.Model = request.Model;

                // 累积 function call 增量
                var deltas = chunk.Choices?.FirstOrDefault()?.Delta?.ToolCalls;
                if (deltas is not null)
                {
                    foreach (var delta in deltas)
                    {
                        StreamToolCallHelper.MergeDelta(toolCallAccumulators, delta);
                    }
                }

                // 当 finish_reason 为 tool_calls 时，回填完整的 tool calls
                var finishReason = chunk.Choices?.FirstOrDefault()?.FinishReason;
                if (finishReason == "tool_calls" && toolCallAccumulators.Count > 0)
                {
                    var fullToolCalls = StreamToolCallHelper.ToStreamToolCallDeltas(toolCallAccumulators);
                    var firstChoice = chunk.Choices.First();
                    if (firstChoice.Delta is null)
                        firstChoice.Delta = new StreamDelta();
                    firstChoice.Delta.ToolCalls = fullToolCalls;

                    _logger.LogInformation(
                        "StreamAsync 工具调用完成: ToolCount={ToolCount}, Names={Names}",
                        fullToolCalls.Count,
                        string.Join(", ", fullToolCalls.Select(t => t.Function?.Name).Where(n => !string.IsNullOrEmpty(n))));
                }

                emittedAny = true;
                yield return chunk;
            }

            _logger.LogInformation(
                "StreamAsync 流式结束: Model={Model}, EmittedAny={EmittedAny}, AccumulatedTools={AccumulatedTools}",
                request.Model, emittedAny, toolCallAccumulators.Count);
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
                "HttpClient 已配置: BaseAddress={BaseAddress}, Timeout={Timeout}s",
                client.BaseAddress, client.Timeout.TotalSeconds);

            return client;
        }
    }
}
