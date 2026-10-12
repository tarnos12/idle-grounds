# PowerShell front door for tools/bg/*.sh (the scripts themselves need Git Bash).
#   powershell -ExecutionPolicy Bypass -File tools/bg/bg.ps1 <command> [args]
#   commands: sync | compile | unity | test | menu | shot | build | publish-back | task
#   e.g.  .\tools\bg\bg.ps1 test editmode
#         .\tools\bg\bg.ps1 menu "Idle Grounds/Art/Integrate Incoming Art"
#         .\tools\bg\bg.ps1 shot center
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Command,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest
)
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot "$Command.sh"
if (-not (Test-Path $script)) { Write-Host "bg: unknown command '$Command' (no $script)"; exit 64 }
$bash = @("$env:ProgramFiles\Git\bin\bash.exe", "${env:ProgramFiles(x86)}\Git\bin\bash.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $bash) { $cmd = Get-Command bash -ErrorAction SilentlyContinue; if ($cmd) { $bash = $cmd.Source } }
if (-not $bash) { Write-Host "bg: Git Bash not found"; exit 127 }
if ($null -eq $Rest) { $Rest = @() }
& $bash ($script -replace '\\', '/') @Rest
exit $LASTEXITCODE
