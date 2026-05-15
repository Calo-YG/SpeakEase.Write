using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed partial class WebSearchTool(IHttpClientFactory httpClientFactory) : IToolExecutor
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private const string DuckDuckGoHtmlUrl = "https://html.duckduckgo.com/html/";

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "web_search",
            Description = "通过 DuckDuckGo 搜索互联网信息，获取最新资料、事实、新闻等。适用于需要查阅外部信息的场景",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["query"] = new() { Type = "string", Description = "搜索关键词（必填），支持中文和英文" },
                    ["limit"] = new() { Type = "integer", Description = "返回结果数量上限（默认5，范围1-20）" }
                },
                Required = ["query"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var query = args.GetString("query", required: true);
        var limit = args.GetInt32("limit", defaultValue: 5, min: 1, max: 20);
        if (args.HasErrors) return args.ToErrorResult();

        var client = _httpClientFactory.CreateClient("DuckDuckGo");
        client.Timeout = TimeSpan.FromSeconds(15);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["q"] = query,
            ["b"] = ""
        });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(DuckDuckGoHtmlUrl, content, ct);
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail("搜索请求超时，请稍后重试", "timeout");
        }
        catch (HttpRequestException ex)
        {
            return ToolResult.Fail($"搜索请求失败: {ex.Message}", "network_error");
        }

        if (!response.IsSuccessStatusCode)
        {
            return ToolResult.Fail($"搜索引擎返回错误 ({(int)response.StatusCode})", "search_error");
        }

        var html = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(html))
        {
            return ToolResult.Fail("搜索返回空结果", "empty_response");
        }

        var results = ParseSearchResults(html, limit);

        if (results.Count == 0)
        {
            return ToolResult.Fail($"未找到与「{query}」相关的搜索结果", "no_results");
        }

        return ToolResult.Ok(JsonSerializer.Serialize(results));
    }

    private static List<SearchResult> ParseSearchResults(string html, int limit)
    {
        var results = new List<SearchResult>();

        var resultMatches = ResultBlockRegex().Matches(html);

        foreach (Match match in resultMatches)
        {
            if (results.Count >= limit) break;

            var block = match.Value;

            var titleMatch = ResultTitleRegex().Match(block);
            var snippetMatch = ResultSnippetRegex().Match(block);
            var urlMatch = ResultLinkRegex().Match(block);

            var title = titleMatch.Success
                ? WebUtility.HtmlDecode(StripHtmlTags(titleMatch.Groups[1].Value)).Trim()
                : null;

            var snippet = snippetMatch.Success
                ? WebUtility.HtmlDecode(StripHtmlTags(snippetMatch.Groups[1].Value)).Trim()
                : null;

            var url = urlMatch.Success
                ? urlMatch.Groups[1].Value
                : null;

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(snippet))
                continue;

            var cleanedUrl = CleanUrl(url);

            results.Add(new SearchResult
            {
                Title = title ?? "(无标题)",
                Snippet = snippet ?? "",
                Url = cleanedUrl
            });
        }

        return results;
    }

    private static string StripHtmlTags(string html)
    {
        return HtmlTagRegex().Replace(html, " ");
    }

    private static string CleanUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        url = url.Trim();

        if (url.StartsWith("//"))
            url = "https:" + url;

        if (url.StartsWith("/l/?kh=-1&uddg="))
        {
            var decoded = Uri.UnescapeDataString(url.Replace("/l/?kh=-1&uddg=", ""));
            var ampIndex = decoded.IndexOf("&rut=", StringComparison.Ordinal);
            if (ampIndex > 0)
                decoded = decoded[..ampIndex];
            return decoded;
        }

        return url;
    }

    [GeneratedRegex("""<a\s+(?:[^>]*?\s+)?rel="nofollow"\s+class="result__a"[^>]*>([\s\S]*?)</a>""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ResultTitleRegex();

    [GeneratedRegex("""class="result__snippet"[^>]*>([\s\S]*?)</a>""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ResultSnippetRegex();

    [GeneratedRegex("""<a\s+(?:[^>]*?\s+)?rel="nofollow"\s+class="result__a"\s+href="([^"]*)"[^>]*>""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ResultLinkRegex();

    [GeneratedRegex("""<[^>]+>""", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("""<div\s+class="result\s+results_links\s+results_links_deep\s+web-result"[^>]*>[\s\S]*?</div>\s*</div>""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ResultBlockRegex();

    private sealed class SearchResult
    {
        public string Title { get; set; }
        public string Snippet { get; set; }
        public string Url { get; set; }
    }
}
