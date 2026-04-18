namespace AINWZ.Infrastructure.LLM.Options;

/// <summary>
/// PowerShell 命令执行工具配置。
/// </summary>
public sealed class PowerShellToolOptions
{
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "ToolPowerShell";

    /// <summary>
    /// 是否启用 PowerShell 工具；默认 false（安全考虑，需显式开启）。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 命令执行超时时间（秒）；默认 30 秒。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 输出内容最大字符数，超出截断；默认 8000。
    /// </summary>
    public int MaxOutputLength { get; set; } = 8000;

    /// <summary>
    /// 工作目录；为空则使用系统临时目录。
    /// </summary>
    public string WorkingDirectory { get; set; }

    /// <summary>
    /// 是否为只读模式（禁止写文件、删文件、网络请求等）；默认 true。
    /// 只读模式下将在命令前注入约束策略，阻止 Set-Content、Remove-Item、Invoke-WebRequest 等。
    /// </summary>
    public bool ReadOnlyMode { get; set; } = true;

    /// <summary>
    /// 允许执行的命令白名单（不含 .exe 后缀，不区分大小写）。
    /// 为空则允许所有非黑名单命令。优先级高于黑名单。
    /// </summary>
    public List<string> AllowedCommands { get; set; } = new();

    /// <summary>
    /// 禁止执行的命令黑名单（不区分大小写）。
    /// 默认包含高危命令。
    /// </summary>
    public List<string> BlockedCommands { get; set; } = new()
    {
        "Remove-Item", "rm", "del", "rmdir",
        "Stop-Process", "kill",
        "Restart-Computer", "Stop-Computer", "Shutdown",
        "Format-Volume", "Clear-Disk",
        "Invoke-Expression", "iex",
        "New-Service", "Remove-Service",
        "Set-ExecutionPolicy",
        "reg", "regedit",
        "net", "netsh",
        "schtasks",
        "certutil"
    };
}
