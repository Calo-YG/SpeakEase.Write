using System.Text.Json;
using System.Text.Json.Serialization;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using AINWZ.Infrastructure.LLM.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AINWZ.Infrastructure.LLM.ToolHandlers;

/// <summary>
/// 通过搜索网关执行网页搜索的内置工具。
/// <para>支持 Bing Web Search API 及通用 POST JSON 网关，含超时、重试、内容截断、结构化输出及日志。</para>
/// </summary>
public sealed class WebSearchToolHandler : ILLMToolHandler
{
    private readonly HttpClient _httpClient;
    private readonly ToolSearchOptions _options;
    private readonly ILogger<WebSearchToolHandler> _logger;

    public WebSearchToolHandler(
        HttpClient httpClient,
        IOptions<ToolSearchOptions> options,
        ILogger<WebSearchToolHandler> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "web_search";

    /// <inheritdoc />
    public LLMToolDefinition ToolDefinition => new()
    {
        Type = "function",
        Function = new LLMToolFunctionDefinition
        {
            Name = Name,
            Description = "通过搜索引擎搜索网页内容，返回相关结果。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "搜索关键词" },
                    count = new { type = "integer", description = "返回结果数量，默认5" }
                },
                required = new[] { "query" }
            }
        }
    };

    /// <inheritdoc />
    public async Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var input = JsonSerializer.Deserialize<WebSearchArguments>(arguments, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new WebSearchArguments();

        // 1. 参数校验
        if (string.IsNullOrWhiteSpace(input.Query))
        {
            return Failure("missing_query", "query 不能为空。");
        }

        // 2. 配置校验
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            return Failure("tool_not_configured", "未配置 ToolSearch:Endpoint，web_search 暂不可用。");
        }

        // 3. MaxResults 限制
        var maxResultsLimit = Math.Clamp(_options.MaxResultsLimit, 1, 20);
        var defaultMaxResults = _options.DefaultMaxResults > 0 ? _options.DefaultMaxResults : 5;
        var maxResults = input.MaxResults is >= 1
            ? Math.Min(input.MaxResults.Value, maxResultsLimit)
            : Math.Min(defaultMaxResults, maxResultsLimit);

        // 4. 超时控制
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = _options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 15;
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        _logger.LogDebug("web_search 开始: Provider={Provider}, Query={Query}, MaxResults={MaxResults}, Timeout={Timeout}s",
            _options.Provider, input.Query, maxResults, timeoutSeconds);

        try
        {
            return _options.Provider == SearchProvider.Bing
                ? await ExecuteBingAsync(input.Query, maxResults, cts.Token)
                : await ExecuteGenericAsync(input.Query, maxResults, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("web_search 超时: Query={Query}, Timeout={Timeout}s", input.Query, timeoutSeconds);
            return Failure("search_timeout", $"搜索超时（{timeoutSeconds}秒），请缩短查询或稍后重试。");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "web_search 网络错误: Query={Query}", input.Query);
            return Failure("search_network_error", $"网络错误: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "web_search 未知错误: Query={Query}", input.Query);
            return Failure("search_unknown_error", $"搜索执行失败: {ex.Message}");
        }
    }

    // ==================== Bing Web Search API ====================

    private async Task<LLMToolExecutionResult> ExecuteBingAsync(string query, int maxResults, CancellationToken ct)
    {
        // 构建请求 URL（Bing 使用 GET + query string）
        var url = BuildBingRequestUrl(query, maxResults);

        var (content, statusCode) = await SendWithRetryAsync(HttpMethod.Get, url, null, ct);

        if (statusCode != 200)
        {
            _logger.LogWarning("web_search Bing 返回错误: StatusCode={StatusCode}, Body={Body}",
                statusCode, TruncateForLog(content, 500));
            return Failure("search_request_failed", $"Bing 搜索返回 {statusCode}: {TruncateForLog(content, 500)}");
        }

        // 解析 Bing 响应
        var searchResults = ParseBingResponse(content, maxResults);

        // 结构化输出
        var payload = JsonSerializer.Serialize(new
        {
            provider = "bing",
            query,
            maxResults,
            resultCount = searchResults.Count,
            results = searchResults
        });

        var maxContentLength = _options.MaxContentLength > 0 ? _options.MaxContentLength : 4000;
        var truncated = false;
        if (payload.Length > maxContentLength)
        {
            payload = payload[..maxContentLength];
            truncated = true;
        }

        _logger.LogDebug("web_search Bing 完成: Query={Query}, Results={ResultCount}, Truncated={Truncated}",
            query, searchResults.Count, truncated);

        return new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            Content = payload
        };
    }

    private string BuildBingRequestUrl(string query, int maxResults)
    {
        var endpoint = _options.Endpoint.TrimEnd('/');
        var queryParams = new List<string>
        {
            $"q={Uri.EscapeDataString(query)}",
            $"count={maxResults}"
        };

        if (!string.IsNullOrWhiteSpace(_options.Language) || !string.IsNullOrWhiteSpace(_options.Country))
        {
            // Bing 使用 mkt 参数（如 zh-CN, en-US）
            var mkt = !string.IsNullOrWhiteSpace(_options.Language) ? _options.Language : "en-US";
            queryParams.Add($"mkt={Uri.EscapeDataString(mkt)}");
        }

        if (_options.SafeSearch)
        {
            queryParams.Add("safeSearch=Moderate");
        }
        else
        {
            queryParams.Add("safeSearch=Off");
        }

        // responseFilter 只请求网页结果
        queryParams.Add("responseFilter=Webpages");

        return $"{endpoint}?{string.Join("&", queryParams)}";
    }

    private List<BingSearchResult> ParseBingResponse(string content, int maxResults)
    {
        var results = new List<BingSearchResult>();

        try
        {
            var response = JsonSerializer.Deserialize<BingSearchResponse>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (response?.WebPages?.Value is { } pages)
            {
                foreach (var page in pages.Take(maxResults))
                {
                    results.Add(new BingSearchResult
                    {
                        Name = page.Name ?? string.Empty,
                        Url = page.Url ?? string.Empty,
                        Snippet = page.Snippet ?? string.Empty,
                        DateLastCrawled = page.DateLastCrawled
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "web_search Bing 响应解析失败，返回原始内容");
        }

        return results;
    }

    // ==================== 通用 POST JSON 网关 ====================

    private async Task<LLMToolExecutionResult> ExecuteGenericAsync(string query, int maxResults, CancellationToken ct)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["query"] = query,
            ["maxResults"] = maxResults
        };
        if (!string.IsNullOrWhiteSpace(_options.Language))
        {
            requestBody["language"] = _options.Language;
        }
        if (!string.IsNullOrWhiteSpace(_options.Country))
        {
            requestBody["country"] = _options.Country;
        }
        if (_options.SafeSearch)
        {
            requestBody["safeSearch"] = true;
        }

        var json = JsonSerializer.Serialize(requestBody);

        var (content, statusCode) = await SendWithRetryAsync(HttpMethod.Post, _options.Endpoint, json, ct);

        if (statusCode != 200)
        {
            _logger.LogWarning("web_search Generic 返回错误: StatusCode={StatusCode}, Body={Body}",
                statusCode, TruncateForLog(content, 500));
            return Failure("search_request_failed", $"搜索网关返回 {statusCode}: {TruncateForLog(content, 500)}");
        }

        // 内容截断
        var maxContentLength = _options.MaxContentLength > 0 ? _options.MaxContentLength : 4000;
        var truncated = false;
        if (content.Length > maxContentLength)
        {
            content = content[..maxContentLength];
            truncated = true;
        }

        var payload = JsonSerializer.Serialize(new
        {
            provider = "generic",
            query,
            maxResults,
            truncated,
            contentLength = content.Length,
            results = content
        });

        _logger.LogDebug("web_search Generic 完成: Query={Query}, Truncated={Truncated}, ContentLength={Length}",
            query, truncated, content.Length);

        return new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            Content = payload
        };
    }

    // ==================== 通用请求 + 重试 ====================

    private async Task<(string Content, int StatusCode)> SendWithRetryAsync(
        HttpMethod method, string url, string? body, CancellationToken ct)
    {
        var retryCount = Math.Clamp(_options.RetryCount, 0, 5);

        for (var attempt = 0; attempt <= retryCount; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogDebug("web_search 重试 {Retry}/{MaxRetry}", attempt, retryCount);
                if (_options.RetryIntervalMs > 0)
                {
                    await Task.Delay(_options.RetryIntervalMs, ct);
                }
            }

            using var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            }

            // 鉴权头
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                var headerName = string.IsNullOrWhiteSpace(_options.ApiKeyHeaderName) ? "Authorization" : _options.ApiKeyHeaderName;
                var headerValue = string.IsNullOrWhiteSpace(_options.ApiKeyHeaderPrefix)
                    ? _options.ApiKey
                    : $"{_options.ApiKeyHeaderPrefix} {_options.ApiKey}";
                request.Headers.TryAddWithoutValidation(headerName, headerValue);
            }

            using var response = await _httpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return (content, (int)response.StatusCode);
            }

            // 最后一次失败直接返回
            if (attempt == retryCount)
            {
                return (content, (int)response.StatusCode);
            }
        }

        return (string.Empty, 0);
    }

    // ==================== 辅助方法 ====================

    private LLMToolExecutionResult Failure(string errorCode, string message)
    {
        return new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = false,
            ErrorCode = errorCode,
            Content = message
        };
    }

    private static string TruncateForLog(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
        return $"{text[..maxLength]}…（已截断）";
    }

    // ==================== 请求/响应模型 ====================

    private sealed class WebSearchArguments
    {
        public string Query { get; set; } = string.Empty;

        public int? MaxResults { get; set; }
    }

    private sealed class BingSearchResponse
    {
        [JsonPropertyName("webPages")]
        public BingWebPages WebPages { get; set; }
    }

    private sealed class BingWebPages
    {
        [JsonPropertyName("value")]
        public List<BingWebPage> Value { get; set; }

        [JsonPropertyName("totalEstimatedMatches")]
        public long? TotalEstimatedMatches { get; set; }
    }

    private sealed class BingWebPage
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("snippet")]
        public string Snippet { get; set; }

        [JsonPropertyName("dateLastCrawled")]
        public string DateLastCrawled { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }
    }
}

/// <summary>
/// Bing 搜索结果项。
/// </summary>
public sealed class BingSearchResult
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Snippet { get; set; } = string.Empty;

    public string? DateLastCrawled { get; set; }
}
