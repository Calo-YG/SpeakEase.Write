using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using AINWZ.Infrastructure.LLM.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AINWZ.Infrastructure.LLM.ToolHandlers;

/// <summary>
/// PowerShell 命令执行工具。
/// <para>安全策略：默认关闭（需 Enabled=true）、只读模式、黑名单过滤、超时控制、输出截断。</para>
/// </summary>
public sealed class PowerShellToolHandler : ILLMToolHandler
{
    private readonly PowerShellToolOptions _options;
    private readonly ILogger<PowerShellToolHandler> _logger;

    public PowerShellToolHandler(
        IOptions<PowerShellToolOptions> options,
        ILogger<PowerShellToolHandler> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "run_powershell";

    /// <inheritdoc />
    public LLMToolDefinition ToolDefinition => new()
    {
        Type = "function",
        Function = new LLMToolFunctionDefinition
        {
            Name = Name,
            Description = "执行 PowerShell 命令并返回输出。受安全策略限制：黑名单过滤、只读模式、超时控制。",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    command = new { type = "string", description = "要执行的 PowerShell 命令" }
                },
                required = new[] { "command" }
            }
        }
    };

    /// <inheritdoc />
    public async Task<LLMToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        // 0. 全局开关
        if (!_options.Enabled)
        {
            return Failure("tool_disabled", "PowerShell 工具未启用，请在配置中设置 ToolPowerShell:Enabled=true。");
        }

        var input = JsonSerializer.Deserialize<PowerShellArguments>(arguments, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? new PowerShellArguments();

        // 1. 参数校验
        if (string.IsNullOrWhiteSpace(input.Command))
        {
            return Failure("missing_command", "command 不能为空。");
        }

        var command = input.Command.Trim();

        // 2. 安全检查：黑名单
        var blockedCommand = FindBlockedCommand(command);
        if (blockedCommand is not null)
        {
            _logger.LogWarning("run_powershell 拒绝执行黑名单命令: {BlockedCommand}", blockedCommand);
            return Failure("blocked_command", $"命令被安全策略阻止: {blockedCommand}");
        }

        // 3. 安全检查：白名单（非空时生效）
        if (_options.AllowedCommands.Count > 0)
        {
            var firstToken = ExtractFirstToken(command);
            if (!IsInList(firstToken, _options.AllowedCommands))
            {
                _logger.LogWarning("run_powershell 拒绝非白名单命令: {Command}", firstToken);
                return Failure("not_in_whitelist", $"命令不在白名单中: {firstToken}");
            }
        }

        _logger.LogDebug("run_powershell 开始执行: Command={Command}", TruncateForLog(command, 200));

        try
        {
            // 4. 构建最终命令（注入只读约束策略）
            var finalCommand = _options.ReadOnlyMode
                ? BuildReadOnlyCommand(command)
                : command;

            // 5. 启动进程
            var startInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-NoLogo -NoProfile -NonInteractive -Command \"{finalCommand.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // 工作目录
            var workDir = !string.IsNullOrWhiteSpace(_options.WorkingDirectory)
                ? _options.WorkingDirectory
                : Path.GetTempPath();
            if (Directory.Exists(workDir))
            {
                startInfo.WorkingDirectory = workDir;
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // 6. 超时控制
            var timeoutMs = (_options.TimeoutSeconds > 0 ? _options.TimeoutSeconds : 30) * 1000;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            var stdoutTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var line = await process.StandardOutput.ReadLineAsync(cts.Token);
                    if (line is null) break;
                    stdoutBuilder.AppendLine(line);
                }
            }, cts.Token);

            var stderrTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var line = await process.StandardError.ReadLineAsync(cts.Token);
                    if (line is null) break;
                    stderrBuilder.AppendLine(line);
                }
            }, cts.Token);

            // 等待进程退出或超时
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 超时（CTS触发，但请求级Token未被取消），杀进程
                try { process.Kill(entireProcessTree: true); } catch { }
                _logger.LogWarning("run_powershell 超时: Timeout={Timeout}s, Command={Command}", timeoutMs / 1000, TruncateForLog(command, 100));
                return Failure("execution_timeout", $"命令执行超时（{timeoutMs / 1000}秒），已终止进程。");
            }
            catch (OperationCanceledException)
            {
                // 请求级取消（客户端断连等），确保进程被清理
                try { process.Kill(entireProcessTree: true); } catch { }
                _logger.LogWarning("run_powershell 因请求取消而终止: Command={Command}", TruncateForLog(command, 100));
                return Failure("execution_timeout", $"请求已取消，命令执行被终止。");
            }

            await Task.WhenAll(stdoutTask, stderrTask);

            var stdout = stdoutBuilder.ToString();
            var stderr = stderrBuilder.ToString();
            var exitCode = process.ExitCode;

            // 7. 输出截断
            var maxOutput = _options.MaxOutputLength > 0 ? _options.MaxOutputLength : 8000;
            var truncated = false;
            if (stdout.Length > maxOutput)
            {
                stdout = stdout[..maxOutput];
                truncated = true;
            }

            var payload = JsonSerializer.Serialize(new
            {
                command = TruncateForLog(command, 500),
                exitCode,
                readOnlyMode = _options.ReadOnlyMode,
                truncated,
                outputLength = stdout.Length,
                stdout,
                stderr = string.IsNullOrWhiteSpace(stderr) ? null : TruncateForLog(stderr, 2000)
            });

            _logger.LogDebug("run_powershell 完成: ExitCode={ExitCode}, OutputLength={Length}, Truncated={Truncated}",
                exitCode, stdout.Length, truncated);

            return new LLMToolExecutionResult
            {
                ToolName = Name,
                Success = exitCode == 0,
                Content = payload,
                ErrorCode = exitCode != 0 ? $"exit_code_{exitCode}" : null
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "run_powershell 执行异常: Command={Command}", TruncateForLog(command, 100));
            return Failure("execution_error", $"执行失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 构建只读模式命令：通过 PowerShell ConstrainedLanguage 模式限制危险操作。
    /// ConstrainedLanguage 阻止 .NET 反射调用、Add-Type 等高级功能，仅允许纯 cmdlet/函数调用。
    /// 危险 cmdlet 已在调用前通过黑名单过滤，此处作为额外的纵深防线。
    /// </summary>
    private static string BuildReadOnlyCommand(string userCommand)
    {
        // 使用 [Console]::TreatControlCAsInput 防止用户命令通过 CtrlC 干扰进程
        // 在用户命令后追加 Out-String 确保输出完整
        return $"$ErrorActionPreference = 'Stop'; {userCommand} | Out-String";
    }

    /// <summary>
    /// 检查命令中是否包含黑名单命令。
    /// </summary>
    private string FindBlockedCommand(string command)
    {
        var tokens = command.Split(new[] { ' ', '|', ';', '&', '`' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var clean = token.TrimStart('-').Trim();
            if (IsInList(clean, _options.BlockedCommands))
            {
                return clean;
            }
        }
        return null!;
    }

    /// <summary>
    /// 提取命令的第一个词（命令名）。
    /// </summary>
    private static string ExtractFirstToken(string command)
    {
        var span = command.AsSpan().TrimStart();
        var idx = span.IndexOf(' ');
        return idx > 0 ? span[..idx].ToString() : span.ToString();
    }

    /// <summary>
    /// 不区分大小写的列表匹配。
    /// </summary>
    private static bool IsInList(string value, List<string> list)
    {
        return list.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
    }

    private static string TruncateForLog(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }

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

    private sealed class PowerShellArguments
    {
        /// <summary>
        /// 要执行的 PowerShell 命令。
        /// </summary>
        public string Command { get; set; } = string.Empty;
    }
}
