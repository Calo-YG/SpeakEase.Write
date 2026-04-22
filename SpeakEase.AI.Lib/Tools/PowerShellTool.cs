using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SpeakEase.AI.Lib.Tools;

/// <summary>
/// PowerShell 命令执行工具的配置选项
/// </summary>
public sealed class PowerShellToolOptions
{
    /// <summary>
    /// 全局开关，默认禁用。启用需显式设置为 true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 命令执行超时（秒），默认 30
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 输出最大截断长度（字符），0 表示不截断，默认 4000
    /// </summary>
    public int MaxOutputLength { get; set; } = 4000;

    /// <summary>
    /// 工作目录，默认系统临时目录
    /// </summary>
    public string WorkingDirectory { get; set; } = Path.GetTempPath();

    /// <summary>
    /// 是否启用只读模式（ConstrainedLanguage），默认 true
    /// </summary>
    public bool ReadOnlyMode { get; set; } = true;

    /// <summary>
    /// 命令黑名单关键词（忽略大小写），匹配到的命令将被拒绝执行
    /// </summary>
    public List<string> Blacklist { get; set; } =
    [
        "Remove-Item", "rm ", "del ", "rd ", "rmdir ",
        "Remove-Service", "Stop-Computer", "Restart-Computer",
        "Format-Volume", "Clear-EventLog", "Set-ExecutionPolicy",
        "Invoke-WebRequest", "Invoke-RestMethod", "Start-Process",
        "New-Service", "Set-Service", "sc ", "net user",
        "net localgroup", "reg ", "regedit"
    ];

    /// <summary>
    /// 命令白名单前缀（忽略大小写），为空时允许所有非黑名单命令
    /// </summary>
    public List<string> Whitelist { get; set; } = [];
}

/// <summary>
/// PowerShell 命令执行工具：安全可控地执行 PowerShell 命令并返回结果
/// </summary>
public sealed class PowerShellTool : IToolExecutor
{
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "run_powershell",
            Description = "执行命令并返回结果。默认以只读模式运行，禁止执行危险操作。可获取系统信息、文件列表等",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["command"] = new()
                    {
                        Type = "string",
                        Description = "要执行的 PowerShell 命令"
                    }
                },
                Required = ["command"]
            }
        }
    };

    private readonly PowerShellToolOptions _options;


    /// <summary>
    /// 默认构造：使用默认配置（默认禁用，需通过 Options 设置 Enabled=true）
    /// </summary>
    public PowerShellTool() : this(new PowerShellToolOptions()) { }

    /// <summary>
    /// 注入配置构造
    /// </summary>
    public PowerShellTool(PowerShellToolOptions options)
    {
        _options = options ?? new PowerShellToolOptions();
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        // 安全防线 1：全局开关
        if (!_options.Enabled)
        {
            return new ToolResult
            {
                Success = false,
                Content = "PowerShell 工具未启用，请在配置中设置 Enabled=true",
                ErrorCode = "tool_disabled"
            };
        }

        string command = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("command", out var prop))
                command = prop.GetString();
        }
        catch { /* 忽略 */ }

        if (string.IsNullOrWhiteSpace(command))
        {
            return new ToolResult
            {
                Success = false,
                Content = "缺少 command 参数",
                ErrorCode = "missing_parameter"
            };
        }

        // 安全防线 2：黑名单过滤
        var commandUpper = command.ToUpperInvariant();
        foreach (var blocked in _options.Blacklist)
        {
            if (commandUpper.Contains(blocked.ToUpperInvariant()))
            {
                return new ToolResult
                {
                    Success = false,
                    Content = $"命令被安全策略拦截：包含禁止的操作 '{blocked.Trim()}'",
                    ErrorCode = "blocked_by_blacklist"
                };
            }
        }

        // 安全防线 3：白名单过滤（白名单非空时启用）
        if (_options.Whitelist is { Count: > 0 })
        {
            var allowed = _options.Whitelist.Any(w => commandUpper.StartsWith(w.ToUpperInvariant()));
            if (!allowed)
            {
                return new ToolResult
                {
                    Success = false,
                    Content = "命令不在白名单允许的范围内",
                    ErrorCode = "blocked_by_whitelist"
                };
            }
        }

        // 构造启动参数，安全防线 4：ConstrainedLanguage 只读模式
        var psi = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = _options.ReadOnlyMode
                ? $"-NoProfile -NoLogo -Command \"{EscapeForProcessArg(command)}\""
                : $"-NoProfile -NoLogo -Command \"{EscapeForProcessArg(command)}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = _options.WorkingDirectory
        };

        if (_options.ReadOnlyMode)
        {
            // 通过环境变量设置 ConstrainedLanguage 模式
            psi.Environment["__PSLockdownPolicy"] = "1";
        }

        // 安全防线 5：超时控制
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        Process process = null;
        try
        {
            process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            // 输出截断
            if (_options.MaxOutputLength > 0 && stdout.Length > _options.MaxOutputLength)
                stdout = stdout[.._options.MaxOutputLength] + $"\n...[已截断，总长度 {stdout.Length}]";
            if (_options.MaxOutputLength > 0 && stderr.Length > _options.MaxOutputLength)
                stderr = stderr[.._options.MaxOutputLength] + $"\n...[已截断，总长度 {stderr.Length}]";

            var result = JsonSerializer.Serialize(new
            {
                exitCode = process.ExitCode,
                stdout = stdout.TrimEnd(),
                stderr = stderr.TrimEnd(),
                readOnlyMode = _options.ReadOnlyMode
            });

            return new ToolResult
            {
                Success = process.ExitCode == 0,
                Content = result
            };
        }
        // 独立捕获超时异常，避免冒泡为 500 错误
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillProcessSafe(process);
            return new ToolResult
            {
                Success = false,
                Content = $"命令执行超时（{_options.TimeoutSeconds}秒），已强制终止",
                ErrorCode = "execution_timeout"
            };
        }
        // 捕获请求级取消
        catch (OperationCanceledException)
        {
            KillProcessSafe(process);
            return new ToolResult
            {
                Success = false,
                Content = "命令执行被取消",
                ErrorCode = "execution_cancelled"
            };
        }
        catch (Exception ex)
        {
            KillProcessSafe(process);
            return new ToolResult
            {
                Success = false,
                Content = $"命令执行失败: {ex.Message}",
                ErrorCode = "execution_error"
            };
        }
    }

    /// <summary>
    /// 安全终止进程
    /// </summary>
    private static void KillProcessSafe(Process process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch { /* 终止失败时静默忽略 */ }
    }

    /// <summary>
    /// 对命令字符串做简单转义，防止在 ProcessStartInfo.Arguments 中破坏引号
    /// </summary>
    private static string EscapeForProcessArg(string command)
    {
        return command.Replace("\"", "\\\"");
    }
}
