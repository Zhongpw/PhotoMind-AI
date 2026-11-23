[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# PhotoMind AI - test script (offline source). Phase 0/1 has no test project yet;
# once xUnit projects are added this runs them directly.
$root = Split-Path -Parent $PSScriptRoot

$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:APPDATA = Join-Path $root '.home\AppData\Roaming'
$env:LOCALAPPDATA = Join-Path $root '.home\AppData\Local'
$env:TEMP = Join-Path $root '.home\Temp'
$env:TMP = $env:TEMP
$env:NUGET_PACKAGES = Join-Path $root '.nuget\packages'

$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$sln = Join-Path $root 'PhotoMindAI.sln'

Write-Host "Running tests for $sln..." -ForegroundColor Cyan
& $dotnet test $sln /p:RestoreConfigFile=nuget.offline.config /p:MSBuildEnableWorkloadResolver=false -m:1 -v:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Tests completed." -ForegroundColor Green
