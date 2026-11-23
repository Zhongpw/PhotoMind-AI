[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

# PhotoMind AI - offline build script (uses .nuget/offline source).
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

Write-Host "Building $sln ($Configuration) using offline source..." -ForegroundColor Cyan

& $dotnet msbuild $sln /t:Build /p:Configuration=$Configuration /p:RestoreConfigFile=nuget.offline.config /p:MSBuildEnableWorkloadResolver=false -m:1 -nodeReuse:false -v:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Build succeeded." -ForegroundColor Green
Write-Host "Output: src\PhotoMind.App\bin\$Configuration\net8.0-windows\PhotoMind.App.exe"
