namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// 内置 web_search 工具配置。
/// </summary>
public sealed class ToolSearchOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "ToolSearch";

    /// <summary>
    /// 搜索网关地址；为空时 web_search 工具不可用。
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// 搜索网关鉴权令牌。
    /// </summary>
    public string ApiKey { get; set; }
}
