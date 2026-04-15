using System.Text;
using System.Text.Json;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using Microsoft.Extensions.Hosting;

namespace AINWZ.Infrastructure.LLM.ToolHandlers;

/// <summary>
/// 读取 wwwroot 中文件并返回摘要的内置工具。
/// </summary>
public sealed class ReadFileSummaryToolHandler : ILLMToolHandler
{
    private readonly string _rootPath;

    /// <summary>
    /// 初始化处理器。
    /// </summary>
    public ReadFileSummaryToolHandler(IHostEnvironment hostEnvironment)
    {
        _rootPath = Path.Combine(hostEnvironment.ContentRootPath, "wwwroot");
    }

    /// <inheritdoc />
    public string Name => "read_file_summary";

    /// <inheritdoc />
    public async Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var input = JsonSerializer.Deserialize<ReadFileSummaryArguments>(arguments, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new ReadFileSummaryArguments();

        if (string.IsNullOrWhiteSpace(input.Path))
        {
            return new LLMToolExecutionResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "missing_path",
                Content = "path 不能为空。"
            };
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, input.Path));
        var normalizedRoot = Path.GetFullPath(_rootPath);

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return new LLMToolExecutionResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "path_out_of_root",
                Content = "仅允许读取 wwwroot 目录内的文件。"
            };
        }

        if (!File.Exists(fullPath))
        {
            return new LLMToolExecutionResult
            {
                ToolName = Name,
                Success = false,
                ErrorCode = "file_not_found",
                Content = $"文件不存在: {input.Path}"
            };
        }

        var maxLines = input.MaxLines is > 0 and <= 200 ? input.MaxLines.Value : 30;
        var maxChars = input.MaxChars is > 0 and <= 12000 ? input.MaxChars.Value : 2000;

        var builder = new StringBuilder();
        using var reader = new StreamReader(fullPath, Encoding.UTF8, true);
        for (var i = 0; i < maxLines; i++)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            builder.AppendLine(line);
            if (builder.Length >= maxChars)
            {
                break;
            }
        }

        var content = builder.ToString();
        if (content.Length > maxChars)
        {
            content = content[..maxChars];
        }

        var payload = JsonSerializer.Serialize(new
        {
            path = input.Path,
            fullPath,
            preview = content,
            truncated = builder.Length >= maxChars
        });

        return new LLMToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            Content = payload
        };
    }

    private sealed class ReadFileSummaryArguments
    {
        public string Path { get; set; }

        public int? MaxLines { get; set; }

        public int? MaxChars { get; set; }
    }
}
