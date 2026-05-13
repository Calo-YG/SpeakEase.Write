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

        private const string HttpClientName = "SpeakEase.LLM";

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

            using var httpRequest = CreateRequestMessage("chat/completions", request);
            using var response = await GetClient().SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<ChatCompletionResponse>(
                responseStream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("LLM 响应反序列化失败。");

            if (result.Error != null)
            {
                var errorMsg = FormatError(result.Error);
                _logger.LogWarning("ChatAsync LLM 返回协议错误: {Error}", errorMsg);
                return new LLMTurnResult
                {
                    Content = errorMsg,
                    Model = result.Model ?? request.Model
                };
            }

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
                ReasoningContent = firstChoice?.Message?.ReasoningContent,
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

            using var httpRequest = CreateRequestMessage("chat/completions", request);
            using var response = await GetClient().SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            _logger.LogInformation("StreamAsync 流式连接已建立: Model={Model}, StatusCode={StatusCode}",
                request.Model, (int)response.StatusCode);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var contentBuilder = new StringBuilder();
            var reasoningBuilder = new StringBuilder();
            var toolCallAccumulators = new Dictionary<int, ToolCallAccumulator>();
            string finishReason = null;
            string responseModel = context.Model;
            UsageInfo usage = null;
            bool anySseData = false;

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

                if (chunk.Error != null)
                {
                    var errorMsg = FormatError(chunk.Error);
                    _logger.LogWarning("StreamAsync LLM 返回协议错误: {Error}", errorMsg);
                    contentBuilder.Append(errorMsg);
                    yield return new LLMTurnChunk { Type = "content", Content = errorMsg };
                    anySseData = true;
                    continue;
                }

                anySseData = true;

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

                if (!string.IsNullOrEmpty(delta.ReasoningContent))
                {
                    reasoningBuilder.Append(delta.ReasoningContent);
                    yield return new LLMTurnChunk
                    {
                        Type = "reasoning",
                        Content = delta.ReasoningContent
                    };
                }

                if (!string.IsNullOrEmpty(delta.Content))
                {
                    contentBuilder.Append(delta.Content);
                    yield return new LLMTurnChunk
                    {
                        Type = "content",
                        Content = delta.Content
                    };
                }

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

            var hasToolCalls = toolCallAccumulators.Count > 0 &&
                (finishReason == "tool_calls" || finishReason == null && toolCallAccumulators.Count > 0);

            if (!anySseData)
            {
                _logger.LogWarning("StreamAsync 未收到有效的 SSE 数据, StatusCode={StatusCode}", (int)response.StatusCode);
                var fallback = (int)response.StatusCode >= 400
                    ? $"AI 服务返回错误，状态码 {(int)response.StatusCode} {response.ReasonPhrase}"
                    : "AI 服务未返回有效内容，请稍后重试。";
                yield return new LLMTurnChunk { Type = "content", Content = fallback };
                yield return new LLMTurnChunk { Type = "done", TurnResult = new LLMTurnResult { Content = fallback, Model = responseModel } };
                yield break;
            }

            _logger.LogInformation("StreamAsync 流式结束: Model={Model}, HasToolCalls={HasToolCalls}, AccumulatedTools={AccumulatedTools}",
                responseModel, hasToolCalls, toolCallAccumulators.Count);

            yield return new LLMTurnChunk
            {
                Type = "done",
                TurnResult = new LLMTurnResult
                {
                    Content = contentBuilder.ToString(),
                    ReasoningContent = reasoningBuilder.Length > 0 ? reasoningBuilder.ToString() : null,
                    ToolCalls = hasToolCalls ? StreamToolCallHelper.ToToolCalls(toolCallAccumulators) : null,
                    Model = responseModel,
                    Usage = usage
                }
            };
        }

        private static string FormatError(ErrorInfo error)
        {
            if (error == null) return string.Empty;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(error.Code)) parts.Add($"[{error.Code}]");
            if (!string.IsNullOrEmpty(error.Type)) parts.Add($"({error.Type})");
            if (!string.IsNullOrEmpty(error.Message)) parts.Add(error.Message);
            return string.Join(" ", parts);
        }

        private HttpClient GetClient()
        {
            return _httpClientFactory.CreateClient(HttpClientName);
        }

        private HttpRequestMessage CreateRequestMessage<T>(string path, T body)
        {
            var baseUri = new Uri(_context.Url.TrimEnd('/') + "/");
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, path))
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };

            if (!string.IsNullOrWhiteSpace(_context.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _context.ApiKey);

            return request;
        }

        private ChatCompletionRequest BuildRequest(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            bool stream) => new()
        {
            Model = string.IsNullOrWhiteSpace(context.Model) ? _context.Model : context.Model,
            Messages = messages,
            Tools = tools?.Count > 0 ? new List<ToolDefinition>(tools) : null,
            ToolChoice = context.ToolChoice,
            Temperature = context.Temperature,
            MaxTokens = context.MaxTokens,
            TopP = context.TopP,
            FrequencyPenalty = context.FrequencyPenalty,
            PresencePenalty = context.PresencePenalty,
            Stream = stream
        };
    }
}
