# Plain-.NET sim tests (no Unity editor).  .\run.ps1 = fast;  .\run.ps1 -All = + Slow playthroughs.  UnityOnly always excluded.
param([switch]$All, [Parameter(ValueFromRemainingArguments)]$Rest)
$env:DOTNET_CLI_UI_LANGUAGE = "en"; $env:VSLANG = "1033"
Set-Location $PSScriptRoot
$filter = if ($All) { 'TestCategory!=UnityOnly' } else { 'TestCategory!=Slow&TestCategory!=UnityOnly' }
$sw = [Diagnostics.Stopwatch]::StartNew()
$out = & dotnet test -c Release --nologo --filter $filter -v q @Rest 2>&1 | Out-String
$code = $LASTEXITCODE
($out -split "`n") | Where-Object { $_ -match 'error|Failed |\[FAIL\]' } | Select-Object -First 40
$sum = ($out -split "`n") | Where-Object { $_ -match '^(Passed|Failed)!' } | Select-Object -Last 1
if (-not $sum) { $sum = 'no test summary (build failed?)' }
"$sum | wall $([int]$sw.Elapsed.TotalSeconds)s | filter: $filter"
exit $code
