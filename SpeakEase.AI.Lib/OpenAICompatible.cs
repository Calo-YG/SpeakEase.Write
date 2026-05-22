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

        // HttpClient 命名标识，与 DI 注册时使用的名称一致
        private const string HttpClientName = "SpeakEase.LLM";

        // 全局共享的 JSON 序列化配置：忽略 null 值、使用宽松编码（避免 Unicode 转义）
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

            // 动态解析当前用户的 LLM 配置（API Key、模型、Base URL 等）
            await _context.ResolveAsync(cancellationToken);

            // 构建 OpenAI Chat Completion 请求体
            var request = BuildRequest(context, messages, tools, stream: false);

            _logger.LogDebug(
                "ChatAsync 开始: Model={Model}, Messages={MsgCount}, Tools={ToolCount}",
                request.Model, request.Messages?.Count ?? 0, request.Tools?.Count ?? 0);

            // 发送 HTTP POST 请求到 chat/completions 端点
            using var httpRequest = CreateRequestMessage("chat/completions", request);
            using var response = await GetClient().SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // HTTP 状态码非 2xx，返回错误
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMsg = FormatHttpError(response, errorBody);
                _logger.LogWarning("ChatAsync LLM HTTP 错误: {Error}", errorMsg);
                return new LLMTurnResult
                {
                    Content = errorMsg,
                    Model = request.Model,
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            // 反序列化完整响应体为 ChatCompletionResponse
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<ChatCompletionResponse>(
                responseStream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("LLM 响应反序列化失败。");

            // LLM 返回了协议级错误（如速率限制、内容过滤等）
            if (result.Error != null)
            {
                var errorMsg = FormatError(result.Error);
                _logger.LogWarning("ChatAsync LLM 返回协议错误: {Error}", errorMsg);
                return new LLMTurnResult
                {
                    Content = errorMsg,
                    Model = result.Model ?? request.Model,
                    Success = false,
                    ErrorMessage = errorMsg,
                    RequestId = result.Id
                };
            }

            // 取第一个 choice 作为本轮结果
            var firstChoice = result.Choices?.FirstOrDefault();
            // 判断 LLM 是否请求了工具调用
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
                Usage = result.Usage,
                Success = true,
                FinishReason = firstChoice?.FinishReason,
                RequestId = result.Id
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

            // 动态解析当前用户的 LLM 配置
            await _context.ResolveAsync(cancellationToken);

            // 构建流式请求体（Stream = true）
            var request = BuildRequest(context, messages, tools, stream: true);

            _logger.LogDebug(
                "StreamAsync 开始: Model={Model}, Messages={MsgCount}, Tools={ToolCount}",
                request.Model, request.Messages?.Count ?? 0, request.Tools?.Count ?? 0);

            using var httpRequest = CreateRequestMessage("chat/completions", request);
            using var response = await GetClient().SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // HTTP 状态码非 2xx，通过流式输出错误后终止
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMsg = FormatHttpError(response, errorBody);
                _logger.LogWarning("StreamAsync LLM HTTP 错误: {Error}", errorMsg);
                yield return new LLMTurnChunk { Type = "content", Content = errorMsg };
                yield return new LLMTurnChunk
                {
                    Type = "done",
                    TurnResult = new LLMTurnResult
                    {
                        Content = errorMsg,
                        Model = request.Model,
                        Success = false,
                        ErrorMessage = errorMsg
                    }
                };
                yield break;
            }

            _logger.LogInformation("StreamAsync 流式连接已建立: Model={Model}, StatusCode={StatusCode}",
                request.Model, (int)response.StatusCode);

            // 建立 SSE 流读取器，逐行解析 Server-Sent Events
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // 流式片段累积器的初始化
            var contentBuilder = new StringBuilder();          // 累积文本内容
            var reasoningBuilder = new StringBuilder();        // 累积思考链内容（DeepSeek 等模型）
            var toolCallAccumulators = new Dictionary<int, ToolCallAccumulator>(); // 按 index 累积工具调用 delta
            string finishReason = null;
            string responseModel = context.Model;
            string requestId = null;
            UsageInfo usage = null;
            bool anySseData = false;       // 标记是否收到过有效 SSE 数据
            bool hasProtocolError = false; // 标记是否收到过协议错误
            string protocolError = null;

            // === SSE 行解析循环 ===
            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;

                // 跳过空行和非 data: 行
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
                    continue;

                // 提取 data: 后的 JSON 数据
                var data = line[5..].Trim();
                if (data == "[DONE]")
                    break;

                var chunk = JsonSerializer.Deserialize<ChatCompletionStreamChunk>(data, JsonOptions);
                if (chunk is null)
                    continue;

                // 流式 chunk 包含协议错误（如速率限制、内容过滤）
                if (chunk.Error != null)
                {
                    var errorMsg = FormatError(chunk.Error);
                    _logger.LogWarning("StreamAsync LLM 返回协议错误: {Error}", errorMsg);
                    contentBuilder.Append(errorMsg);
                    hasProtocolError = true;
                    protocolError = errorMsg;
                    yield return new LLMTurnChunk { Type = "content", Content = errorMsg };
                    anySseData = true;
                    continue;
                }

                anySseData = true;

                // 更新请求级元数据（响应中的 Id 和 Model）
                if (!string.IsNullOrEmpty(chunk.Id))
                    requestId = chunk.Id;

                if (!string.IsNullOrEmpty(chunk.Model))
                    responseModel = chunk.Model;

                // 累加流式 Usage（部分 LLM 在流式中间或最后发送 usage chunk）
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

                // 思维链增量（DeepSeek R1 等模型的 reasoning_content）
                if (!string.IsNullOrEmpty(delta.ReasoningContent))
                {
                    reasoningBuilder.Append(delta.ReasoningContent);
                    yield return new LLMTurnChunk
                    {
                        Type = "reasoning",
                        Content = delta.ReasoningContent
                    };
                }

                // 普通文本内容增量
                if (!string.IsNullOrEmpty(delta.Content))
                {
                    contentBuilder.Append(delta.Content);
                    yield return new LLMTurnChunk
                    {
                        Type = "content",
                        Content = delta.Content
                    };
                }

                // 工具调用增量：每个 chunk 中 tool_calls 的 delta 片段需要按 index 合并累积
                if (delta.ToolCalls != null)
                {
                    foreach (var toolCallDelta in delta.ToolCalls)
                    {
                        // 将增量片段合并到累加器中
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

            // 判断是否包含工具调用：累加器有数据且 finishReason 为 tool_calls 或未指定但有累积数据
            var hasToolCalls = toolCallAccumulators.Count > 0 &&
                (finishReason == "tool_calls" || finishReason == null && toolCallAccumulators.Count > 0);

            // 未收到任何有效 SSE 数据（可能返回了空流或非 SSE 格式）
            if (!anySseData)
            {
                _logger.LogWarning("StreamAsync 未收到有效的 SSE 数据, StatusCode={StatusCode}", (int)response.StatusCode);
                var fallback = (int)response.StatusCode >= 400
                    ? $"AI 服务返回错误，状态码 {(int)response.StatusCode} {response.ReasonPhrase}"
                    : "AI 服务未返回有效内容，请稍后重试。";
                yield return new LLMTurnChunk { Type = "content", Content = fallback };
                yield return new LLMTurnChunk
                {
                    Type = "done",
                    TurnResult = new LLMTurnResult
                    {
                        Content = fallback,
                        Model = responseModel,
                        Success = false,
                        ErrorMessage = fallback
                    }
                };
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
                    Usage = usage,
                    Success = !hasProtocolError,
                    ErrorMessage = protocolError,
                    FinishReason = finishReason,
                    RequestId = requestId
                }
            };
        }

        // 格式化 LLM 协议级错误信息，合并 Code + Type + Message
        private static string FormatError(ErrorInfo error)
        {
            if (error == null) return string.Empty;
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(error.Code)) parts.Add($"[{error.Code}]");
            if (!string.IsNullOrEmpty(error.Type)) parts.Add($"({error.Type})");
            if (!string.IsNullOrEmpty(error.Message)) parts.Add(error.Message);
            return string.Join(" ", parts);
        }

        // 格式化 HTTP 级错误信息，截断过长响应体（防止日志溢出）
        private static string FormatHttpError(HttpResponseMessage response, string body)
        {
            var message = $"AI 服务返回错误，状态码 {(int)response.StatusCode} {response.ReasonPhrase}";
            if (string.IsNullOrWhiteSpace(body))
                return message;

            var trimmed = body.Length > 1000 ? body[..1000] : body;
            return $"{message}: {trimmed}";
        }

        private HttpClient GetClient()
        {
            return _httpClientFactory.CreateClient(HttpClientName);
        }

        // 构建 HTTP 请求消息：设置 Base URL + 路径 + JSON 请求体 + Bearer Token 鉴权
        private HttpRequestMessage CreateRequestMessage<T>(string path, T body)
        {
            var baseUri = new Uri(_context.Url.TrimEnd('/') + "/");
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, path))
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };

            // 使用 Bearer Token 方式进行 API Key 鉴权
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
            MaxTokens = ResolveMaxTokens(context.MaxTokens),
            TopP = context.TopP,
            FrequencyPenalty = context.FrequencyPenalty,
            PresencePenalty = context.PresencePenalty,
            Stream = stream
        };

        // 解析最大输出 Token 数：取请求值与全局上限的较小值，确保不超过配额
        private int? ResolveMaxTokens(int? requestedMaxTokens)
        {
            // 全局未配置上限，直接使用请求值
            if (_context.MaxOutputTokens <= 0)
                return requestedMaxTokens;

            // 取 min(请求值, 全局上限)，防止单次请求消耗过多 Token
            if (requestedMaxTokens is > 0)
                return Math.Min(requestedMaxTokens.Value, _context.MaxOutputTokens);

            return _context.MaxOutputTokens;
        }
    }
}
