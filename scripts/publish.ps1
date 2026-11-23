[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [string]$OutputDir = ''
)

$ErrorActionPreference = 'Stop'

# PhotoMind AI - publish script. Phase 0/1: framework-dependent publish.
# Phase 6 will switch to self-contained win-x64 (requires extra runtime packs).
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) {
    $OutputDir = Join-Path $root 'artifacts\publish'
}

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
$appProj = Join-Path $root 'src\PhotoMind.App\PhotoMind.App.csproj'

Write-Host "Publishing PhotoMind.App ($Runtime) -> $OutputDir ..." -ForegroundColor Cyan
& $dotnet publish $appProj -c Release -r $Runtime --self-contained false -o $OutputDir /p:RestoreConfigFile=nuget.offline.config /p:MSBuildEnableWorkloadResolver=false -m:1 -v:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Publish succeeded: $OutputDir" -ForegroundColor Green
