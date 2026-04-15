$ErrorActionPreference = 'Stop'

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $PSScriptRoot 'logs'
$stdoutLog = Join-Path $logDir 'ainwz.stdout.log'
$stderrLog = Join-Path $logDir 'ainwz.stderr.log'

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
Remove-Item $stdoutLog, $stderrLog -ErrorAction SilentlyContinue

Get-Process AINWZ -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process dotnet -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like '*dotnet.exe' } |
    Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep -Seconds 1

dotnet build "$workspaceRoot\AINWZ.slnx"

$process = Start-Process dotnet -ArgumentList 'run --project AINWZ --urls http://127.0.0.1:5184' -WorkingDirectory $workspaceRoot -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog -PassThru

Write-Host "AINWZ started. PID=$($process.Id)"

$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    try {
        $content = Invoke-WebRequest -UseBasicParsing 'http://127.0.0.1:5184/ai/skills' | Select-Object -ExpandProperty Content
        Write-Output $content
        $ready = $true
        break
    }
    catch {
    }
}

if (-not $ready) {
    Write-Host 'AINWZ did not become ready in 30 seconds.'
    if (Test-Path $stdoutLog) {
        Write-Host '--- stdout ---'
        Get-Content $stdoutLog
    }
    if (Test-Path $stderrLog) {
        Write-Host '--- stderr ---'
        Get-Content $stderrLog
    }
    exit 1
}
