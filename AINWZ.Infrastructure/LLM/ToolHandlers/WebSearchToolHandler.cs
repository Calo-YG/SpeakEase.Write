using System.Net.Http.Json;
using System.Text.Json;
using AINWZ.Application.LLM;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using AINWZ.Infrastructure.LLM.Options;
using Microsoft.Extensions.Options;

namespace AINWZ.Infrastructure.LLM.ToolHandlers;

/// <summary>
/// 通过外部搜索网关执行网页搜索的内置工具。
/// </summary>
public sealed class WebSearchToolHandler : ILLMToolHandler
{
    private readonly HttpClient _httpClient;
    private readonly ToolSearchOptions _options;

    /// <summary>
    /// 初始化处理器。
    /// </summary>
    public WebSearchToolHandler(HttpClient httpClient, IOptions<ToolSearchOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => "web_search";

    /// <inheritdoc />
    public async Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var input = JsonSerializer.Deserialize<WebSearchArguments>(arguments, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new WebSearchArguments();

        if (string.IsNullOrWhiteSpace(input.Query))
        {
            return new LLMToolExecutionResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "missing_query",
                Content = "query 不能为空。"
            };
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            return new LLMToolExecutionResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "tool_not_configured",
                Content = "未配置 ToolSearch:Endpoint，web_search 暂不可用。"
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(new
            {
                query = input.Query,
                maxResults = input.MaxResults ?? 5
            })
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new LLMToolExecutionResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "search_request_failed",
                Content = content
            };
        }

        return new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            Content = content
        };
    }

    private sealed class WebSearchArguments
    {
        public string Query { get; set; }

        public int? MaxResults { get; set; }
    }
}
